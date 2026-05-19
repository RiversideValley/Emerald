using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth.JSON;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Modpacks;

public interface IMrPackFileInstaller
{
    Task InstallAsync(
        string mrPackPath,
        string instancePath,
        Game? game = null,
        string? sharedBasePath = null,
        string? recordGamePath = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class MrPackFileInstaller : IMrPackFileInstaller
{
    private static readonly string[] ClientOverridePrefixes = ["overrides/", "client-overrides/"];
    private readonly IMrPackReader _reader;
    private readonly IStoreInstallRecordRepository _records;
    private readonly IStoreSharedContentService _sharedContentService;
    private readonly ILogger<MrPackFileInstaller> _logger;
    private readonly HttpClient _httpClient;

    public MrPackFileInstaller(
        IMrPackReader reader,
        IStoreInstallRecordRepository records,
        IStoreSharedContentService sharedContentService,
        ILogger<MrPackFileInstaller> logger)
        : this(reader, records, sharedContentService, logger, CreateDefaultHttpClient())
    {
    }

    public MrPackFileInstaller(
        IMrPackReader reader,
        IStoreInstallRecordRepository records,
        IStoreSharedContentService sharedContentService,
        ILogger<MrPackFileInstaller> logger,
        HttpClient httpClient)
    {
        _reader = reader;
        _records = records;
        _sharedContentService = sharedContentService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task InstallAsync(
        string mrPackPath,
        string instancePath,
        Game? game = null,
        string? sharedBasePath = null,
        string? recordGamePath = null,
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
            await DownloadManifestFileAsync(
                clientFiles[index],
                manifest,
                instancePath,
                game,
                sharedBasePath,
                recordGamePath ?? instancePath,
                cancellationToken);
            progress?.Report(clientFiles.Length == 0 ? 50 : (index + 1d) / clientFiles.Length * 80d);
        }

        await ExtractOverridesAsync(mrPackPath, instancePath, cancellationToken);
        progress?.Report(100);
    }

    private async Task DownloadManifestFileAsync(
        MrPackFile file,
        MrPackManifest manifest,
        string instancePath,
        Game? game,
        string? sharedBasePath,
        string recordGamePath,
        CancellationToken cancellationToken)
    {
        var destinationPath = MrPackPathGuard.GetSafeDestinationPath(instancePath, file.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (game != null
            && TryResolveSharedContent(file.Path, out var contentType, out var installFolderName)
            && _sharedContentService.IsSharingEnabled(game, contentType, sharedBasePath))
        {
            var itemFile = ToItemFile(file, Path.GetFileName(destinationPath));
            var installResult = await _sharedContentService.InstallAsync(new StoreSharedInstallRequest
            {
                Game = game,
                SharedBasePathOverride = sharedBasePath,
                ContentType = contentType,
                InstallFolderName = installFolderName,
                File = itemFile,
                TargetPath = destinationPath,
                DownloadToPathAsync = (targetPath, _, token) =>
                    DownloadAndVerifyManifestFileToPathAsync(file, targetPath, token),
                CancellationToken = cancellationToken
            });

            if (!string.IsNullOrWhiteSpace(installResult.SharedFilePath)
                && !string.IsNullOrWhiteSpace(installResult.Sha1))
            {
                var recordFilePath = Path.Combine(recordGamePath, Path.GetRelativePath(instancePath, destinationPath));
                SaveModpackStoreRecord(
                    manifest,
                    file,
                    contentType,
                    installFolderName,
                    recordGamePath,
                    recordFilePath,
                    installResult,
                    sharedBasePath ?? game.SharedMinecraftBasePath);
            }

            return;
        }

        await DownloadAndVerifyManifestFileToPathAsync(file, destinationPath, cancellationToken);
    }

    private async Task DownloadAndVerifyManifestFileToPathAsync(
        MrPackFile file,
        string destinationPath,
        CancellationToken cancellationToken)
    {
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
            var actualSha1 = await FileHash.ComputeSha1Async(filePath, cancellationToken);
            if (!actualSha1.Equals(sha1, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SHA-1 mismatch for '{file.Path}'.");
            }
        }

        if (file.Hashes.TryGetValue("sha512", out var sha512))
        {
            var actualSha512 = await FileHash.ComputeHashAsync(SHA512.Create(), filePath, cancellationToken);
            if (!actualSha512.Equals(sha512, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SHA-512 mismatch for '{file.Path}'.");
            }
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

    private void SaveModpackStoreRecord(
        MrPackManifest manifest,
        MrPackFile file,
        StoreContentType contentType,
        string installFolderName,
        string instancePath,
        string recordFilePath,
        StoreSharedInstallResult installResult,
        string? sharedBasePath)
    {
        if (string.IsNullOrWhiteSpace(sharedBasePath))
        {
            return;
        }

        _records.LoadForBasePath(sharedBasePath);
        var records = _records.GetAll().ToList();
        var existing = _records
            .FindByFilePath(contentType, recordFilePath, instancePath)
            .ToArray();

        records.RemoveAll(record => existing.Any(removed => removed.Id == record.Id));
        if (existing.Length > 0)
        {
            _records.Save(records);

            foreach (var existingRecord in existing)
            {
                _sharedContentService.RemoveReferenceAsync(existingRecord, deleteInstanceFile: false).GetAwaiter().GetResult();
            }
        }

        var installRecord = new StoreInstallRecord
        {
            ContentType = contentType,
            GamePath = instancePath,
            ProjectTitle = string.IsNullOrWhiteSpace(manifest.Name)
                ? Path.GetFileNameWithoutExtension(recordFilePath)
                : manifest.Name,
            VersionId = manifest.VersionId,
            VersionName = manifest.VersionId,
            FileName = Path.GetFileName(recordFilePath),
            FilePath = recordFilePath,
            Sha1 = installResult.Sha1 ?? file.Hashes.GetValueOrDefault("sha1"),
            Sha512 = installResult.Sha512 ?? file.Hashes.GetValueOrDefault("sha512"),
            GodFolderHash = string.IsNullOrWhiteSpace(installResult.SharedFilePath) ? null : installResult.Sha1,
            HashAlgorithm = string.IsNullOrWhiteSpace(installResult.SharedFilePath) ? null : "sha1",
            SharedFilePath = installResult.SharedFilePath,
            LinkKind = installResult.LinkKind,
            DownloadUrl = file.Downloads.FirstOrDefault(),
            InstalledAtUtc = DateTimeOffset.UtcNow
        };

        records.Add(installRecord);
        _records.Save(records);
    }

    private static ItemFile ToItemFile(MrPackFile file, string fileName)
        => new()
        {
            Filename = fileName,
            Url = file.Downloads.FirstOrDefault() ?? string.Empty,
            Primary = true,
            Size = file.FileSize > int.MaxValue ? int.MaxValue : (int)file.FileSize,
            Hashes = new Hashes
            {
                Sha1 = file.Hashes.GetValueOrDefault("sha1") ?? string.Empty,
                Sha512 = file.Hashes.GetValueOrDefault("sha512") ?? string.Empty
            }
        };

    private static bool TryResolveSharedContent(
        string relativePath,
        out StoreContentType contentType,
        out string installFolderName)
    {
        var normalized = relativePath.Replace('\\', '/');
        var firstSlash = normalized.IndexOf('/');
        var root = firstSlash < 0 ? normalized : normalized[..firstSlash];

        (contentType, installFolderName) = root.ToLowerInvariant() switch
        {
            "mods" => (StoreContentType.Mod, "mods"),
            "resourcepacks" => (StoreContentType.ResourcePack, "resourcepacks"),
            "datapacks" => (StoreContentType.DataPack, "datapacks"),
            "shaderpacks" => (StoreContentType.Shader, "shaderpacks"),
            "plugins" => (StoreContentType.Plugin, "plugins"),
            _ => (StoreContentType.ModPack, string.Empty)
        };

        return !string.IsNullOrWhiteSpace(installFolderName);
    }

}
