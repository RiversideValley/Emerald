using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Modpacks;

public interface IMrPackFileInstaller
{
    Task InstallAsync(
        string mrPackPath,
        string instancePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class MrPackFileInstaller : IMrPackFileInstaller
{
    private static readonly string[] ClientOverridePrefixes = ["overrides/", "client-overrides/"];
    private readonly IMrPackReader _reader;
    private readonly ILogger<MrPackFileInstaller> _logger;
    private readonly HttpClient _httpClient;

    public MrPackFileInstaller(IMrPackReader reader, ILogger<MrPackFileInstaller> logger)
        : this(reader, logger, CreateDefaultHttpClient())
    {
    }

    public MrPackFileInstaller(IMrPackReader reader, ILogger<MrPackFileInstaller> logger, HttpClient httpClient)
    {
        _reader = reader;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task InstallAsync(
        string mrPackPath,
        string instancePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var manifest = await _reader.ReadAsync(mrPackPath, cancellationToken);
        Directory.CreateDirectory(instancePath);

        var clientFiles = manifest.Files
            .Where(file => file.IsClientEligible)
            .ToArray();

        for (var index = 0; index < clientFiles.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DownloadManifestFileAsync(clientFiles[index], instancePath, cancellationToken);
            progress?.Report(clientFiles.Length == 0 ? 50 : (index + 1d) / clientFiles.Length * 80d);
        }

        await ExtractOverridesAsync(mrPackPath, instancePath, cancellationToken);
        progress?.Report(100);
    }

    private async Task DownloadManifestFileAsync(
        MrPackFile file,
        string instancePath,
        CancellationToken cancellationToken)
    {
        var destinationPath = MrPackPathGuard.GetSafeDestinationPath(instancePath, file.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        Exception? lastError = null;
        foreach (var download in file.Downloads.Where(url => !string.IsNullOrWhiteSpace(url)))
        {
            var tempPath = destinationPath + $".emerald-download-{Guid.NewGuid():N}.tmp";
            try
            {
                await DownloadFileAsync(download, tempPath, cancellationToken);
                await VerifyHashesAsync(file, tempPath, cancellationToken);

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(tempPath, destinationPath);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                TryDelete(tempPath);
                _logger.LogWarning(ex, "Failed to download modpack file {Path} from {Url}.", file.Path, download);
            }
        }

        throw new InvalidOperationException(
            $"Failed to download required modpack file '{file.Path}'.",
            lastError);
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await source.CopyToAsync(destination, cancellationToken);
    }

    private static async Task VerifyHashesAsync(MrPackFile file, string filePath, CancellationToken cancellationToken)
    {
        if (file.Hashes.TryGetValue("sha1", out var sha1))
        {
            var actualSha1 = await ComputeHashAsync(SHA1.Create(), filePath, cancellationToken);
            if (!actualSha1.Equals(sha1, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SHA-1 mismatch for '{file.Path}'.");
            }
        }

        if (file.Hashes.TryGetValue("sha512", out var sha512))
        {
            var actualSha512 = await ComputeHashAsync(SHA512.Create(), filePath, cancellationToken);
            if (!actualSha512.Equals(sha512, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SHA-512 mismatch for '{file.Path}'.");
            }
        }
    }

    private static async Task<string> ComputeHashAsync(
        HashAlgorithm algorithm,
        string filePath,
        CancellationToken cancellationToken)
    {
        using (algorithm)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await algorithm.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    private static async Task ExtractOverridesAsync(
        string mrPackPath,
        string instancePath,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(mrPackPath);

        foreach (var prefix in ClientOverridePrefixes)
        {
            foreach (var entry in archive.Entries.Where(entry =>
                         entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = entry.FullName[prefix.Length..];
                if (string.IsNullOrWhiteSpace(relativePath) || relativePath.EndsWith('/'))
                {
                    continue;
                }

                var destinationPath = MrPackPathGuard.GetSafeDestinationPath(instancePath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                await using var source = entry.Open();
                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                await source.CopyToAsync(destination, cancellationToken);
            }
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Emerald", "1.0"));
        return client;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
