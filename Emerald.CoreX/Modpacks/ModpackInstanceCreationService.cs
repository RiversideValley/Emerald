using System.Net.Http.Headers;
using System.Security.Cryptography;
using CmlLib.Core;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Store.Modrinth.JSON;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Modpacks;

public interface IModpackInstanceCreationService
{
    Task<ModpackProbeResult> ProbeAsync(
        ItemVersion version,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<Game> CreateAsync(
        ModpackInstanceCreationRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class ModpackProbeResult
{
    public required string MrPackPath { get; init; }

    public required ItemFile File { get; init; }

    public required MrPackManifest Manifest { get; init; }

    public string MinecraftVersion => Manifest.GetMinecraftVersion() ?? string.Empty;

    public MrPackLoaderDependency Loader => Manifest.GetLoaderDependency();
}

public sealed class ModpackInstanceCreationRequest
{
    public required string InstanceName { get; init; }

    public required string FolderName { get; init; }

    public required StoreItem Project { get; init; }

    public required ItemVersion Version { get; init; }

    public string? MrPackPath { get; init; }
}

public sealed class ModpackInstanceCreationService : IModpackInstanceCreationService
{
    private readonly Core _core;
    private readonly IMrPackReader _reader;
    private readonly IMrPackFileInstaller _fileInstaller;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ModpackInstanceCreationService> _logger;
    private readonly HttpClient _httpClient;

    public ModpackInstanceCreationService(
        Core core,
        IMrPackReader reader,
        IMrPackFileInstaller fileInstaller,
        INotificationService notificationService,
        ILogger<ModpackInstanceCreationService> logger)
        : this(core, reader, fileInstaller, notificationService, logger, CreateDefaultHttpClient())
    {
    }

    public ModpackInstanceCreationService(
        Core core,
        IMrPackReader reader,
        IMrPackFileInstaller fileInstaller,
        INotificationService notificationService,
        ILogger<ModpackInstanceCreationService> logger,
        HttpClient httpClient)
    {
        _core = core;
        _reader = reader;
        _fileInstaller = fileInstaller;
        _notificationService = notificationService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<ModpackProbeResult> ProbeAsync(
        ItemVersion version,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var file = SelectMrPackFile(version);
        var tempPath = Path.Combine(Path.GetTempPath(), "Emerald", "Modpacks", $"{Guid.NewGuid():N}.mrpack");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        try
        {
            await DownloadMrPackAsync(file, tempPath, progress, cancellationToken);
            var manifest = await _reader.ReadAsync(tempPath, cancellationToken);
            return new ModpackProbeResult
            {
                MrPackPath = tempPath,
                File = file,
                Manifest = manifest
            };
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    public async Task<Game> CreateAsync(
        ModpackInstanceCreationRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_core.BasePath == null)
        {
            throw new InvalidOperationException("Cannot create a modpack instance before the Minecraft path is initialized.");
        }

        if (string.IsNullOrWhiteSpace(request.InstanceName))
        {
            throw new InvalidOperationException("The modpack instance name is required.");
        }

        var folderName = request.FolderName.Trim();
        ValidateInstanceFolderName(folderName);

        var instancesRoot = Path.Combine(_core.BasePath.BasePath, Core.GamesFolderName);
        var finalPath = Path.Combine(instancesRoot, folderName);
        if (Directory.Exists(finalPath) || File.Exists(finalPath))
        {
            throw new InvalidOperationException("The selected instance folder already exists.");
        }

        var ownsMrPack = string.IsNullOrWhiteSpace(request.MrPackPath);
        var probe = ownsMrPack
            ? await ProbeAsync(request.Version, progress, cancellationToken)
            : new ModpackProbeResult
            {
                MrPackPath = request.MrPackPath!,
                File = SelectMrPackFile(request.Version),
                Manifest = await _reader.ReadAsync(request.MrPackPath!, cancellationToken)
            };

        var stagingRoot = Path.Combine(instancesRoot, ".emerald-modpack-staging");
        var stagingPath = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);

        var notification = _notificationService.Create(
            "Creating modpack instance",
            request.InstanceName,
            progress: 0,
            isIndeterminate: false,
            isCancellable: false);

        try
        {
            var loader = probe.Manifest.GetLoaderDependency();
            var mcVersion = probe.Manifest.GetMinecraftVersion()
                            ?? throw new InvalidOperationException("The modpack manifest does not specify a Minecraft version.");

            var version = new Versions.Version
            {
                DisplayName = request.InstanceName.Trim(),
                BasedOn = mcVersion,
                Type = loader.Type,
                ModVersion = loader.Version,
                ReleaseType = "modpack"
            };

            var stagingGame = new Game(new MinecraftPath(stagingPath), version, globalGameSettingsService: _core.GlobalGameSettingsService);

            _notificationService.Update(notification.Id, message: "Installing modpack files...", progress: 10);
            var fileProgress = new Progress<double>(value =>
            {
                var scaled = 10 + (value * 0.85);
                progress?.Report(scaled);
                _notificationService.Update(notification.Id, progress: scaled);
            });
            await _fileInstaller.InstallAsync(
                probe.MrPackPath,
                stagingGame.Path.BasePath,
                stagingGame,
                _core.BasePath.BasePath,
                finalPath,
                fileProgress,
                cancellationToken);

            _notificationService.Update(notification.Id, message: "Finalizing instance...", progress: 96);
            Directory.Move(stagingPath, finalPath);
            TryDeleteDirectory(stagingRoot, onlyIfEmpty: true);

            var game = _core.CreateGame(version, folderName);
            progress?.Report(100);
            _notificationService.Complete(notification.Id, true, $"Created {request.InstanceName.Trim()}");
            return game;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create modpack instance {Name}.", request.InstanceName);
            _notificationService.Complete(notification.Id, false, "Modpack creation failed.", ex);
            TryDeleteDirectory(stagingPath);
            TryDeleteDirectory(stagingRoot, onlyIfEmpty: true);
            TryDeleteDirectory(finalPath);
            throw;
        }
        finally
        {
            if (ownsMrPack)
            {
                TryDeleteFile(probe.MrPackPath);
            }
        }
    }

    private static void ValidateInstanceFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)
            || folderName is "." or ".."
            || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || folderName.Contains('/')
            || folderName.Contains('\\'))
        {
            throw new InvalidOperationException("The selected instance folder name is invalid.");
        }
    }

    private async Task DownloadMrPackAsync(
        ItemFile file,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        {
            await using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                readTotal += read;
                if (totalBytes > 0)
                {
                    progress?.Report(readTotal / (double)totalBytes * 100d);
                }
            }

            await destination.FlushAsync(cancellationToken);
        }

        await VerifyMrPackHashAsync(file, destinationPath, cancellationToken);
    }

    private static ItemFile SelectMrPackFile(ItemVersion version)
    {
        var file = version.Files?.FirstOrDefault(file => file.Primary)
                   ?? version.Files?.FirstOrDefault();

        if (file == null)
        {
            throw new InvalidOperationException("The selected modpack version does not have a downloadable file.");
        }

        if (!file.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected modpack version does not provide a .mrpack file.");
        }

        return file;
    }

    private static async Task VerifyMrPackHashAsync(
        ItemFile file,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (file.Hashes == null
            || (string.IsNullOrWhiteSpace(file.Hashes.Sha1) && string.IsNullOrWhiteSpace(file.Hashes.Sha512)))
        {
            throw new InvalidOperationException("The selected modpack file does not include a hash to verify.");
        }

        if (file.Hashes?.Sha1 is { Length: > 0 } sha1)
        {
            var actual = await FileHash.ComputeSha1Async(filePath, cancellationToken);
            if (!actual.Equals(sha1, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Downloaded modpack failed SHA-1 verification.");
            }
        }

        if (file.Hashes?.Sha512 is { Length: > 0 } sha512)
        {
            var actual = await FileHash.ComputeHashAsync(SHA512.Create(), filePath, cancellationToken);
            if (!actual.Equals(sha512, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Downloaded modpack failed SHA-512 verification.");
            }
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Emerald", "1.0"));
        return client;
    }

    private static void TryDeleteFile(string path)
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

    private static void TryDeleteDirectory(string path, bool onlyIfEmpty = false)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            if (onlyIfEmpty && Directory.EnumerateFileSystemEntries(path).Any())
            {
                return;
            }

            Directory.Delete(path, recursive: !onlyIfEmpty);
        }
        catch
        {
        }
    }
}
