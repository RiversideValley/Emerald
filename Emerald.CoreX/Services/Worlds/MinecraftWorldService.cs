using fNbt;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Worlds;

public enum MinecraftWorldGameMode { Survival, Creative, Adventure, Spectator, Unknown }

public partial class MinecraftWorld : ObservableObject
{
    public string FolderName { get; init; } = string.Empty;
    public string FolderPath { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTimeOffset? LastPlayed { get; init; }
    public MinecraftWorldGameMode GameMode { get; init; }
    public int? Difficulty { get; init; }
    public bool Hardcore { get; init; }
    public bool CheatsEnabled { get; init; }
    public int? DataVersion { get; init; }
    public string? MinecraftVersion { get; init; }
    public long? Seed { get; init; }
    public string? IconPath { get; init; }
    public bool UsedBackupMetadata { get; init; }
    public bool IsReadable { get; init; }
    public bool IsLinked { get; init; }
    public string? Warning { get; init; }
    [ObservableProperty] private long? _sizeBytes;
    [ObservableProperty] private bool _canQuickLaunch;
    [ObservableProperty] private bool _sizeUnavailable;
    public string SizeText => SizeBytes is long size ? FormatSize(size) : SizeUnavailable ? "Unavailable" : "Calculating…";

    partial void OnSizeUnavailableChanged(bool value) => OnPropertyChanged(nameof(SizeText));
    partial void OnSizeBytesChanged(long? value) => OnPropertyChanged(nameof(SizeText));
    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }
}

public interface IMinecraftWorldService
{
    Task<IReadOnlyList<MinecraftWorld>> ScanAsync(Game game, CancellationToken cancellationToken = default);
    Task<long?> CalculateSizeAsync(MinecraftWorld world, CancellationToken cancellationToken = default);
}

public sealed class MinecraftWorldService(ILogger<MinecraftWorldService> logger, Emerald.CoreX.Services.IUiDispatcher? dispatcher = null) : IMinecraftWorldService
{
    private const long MaxMetadataBytes = 16 * 1024 * 1024;
    private readonly SemaphoreSlim _sizeQueue = new(2);

    public async Task<IReadOnlyList<MinecraftWorld>> ScanAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var saves = Path.GetFullPath(Path.Combine(game.Path.BasePath, "saves"));
        if (!Directory.Exists(saves)) return [];
        return await Task.Run(() =>
        {
            var worlds = new List<MinecraftWorld>();
            foreach (var directory in Directory.EnumerateDirectories(saves))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var full = Path.GetFullPath(directory);
                if (!IsContained(saves, full)) continue;
                var info = new DirectoryInfo(full);
                var linked = info.Attributes.HasFlag(System.IO.FileAttributes.ReparsePoint);
                var primary = Path.Combine(full, "level.dat");
                var backup = Path.Combine(full, "level.dat_old");
                if (!File.Exists(primary) && !File.Exists(backup)) continue;
                worlds.Add(ParseWorld(info, primary, backup, linked));
            }
            return (IReadOnlyList<MinecraftWorld>)worlds.OrderByDescending(x => x.LastPlayed).ThenBy(x => x.DisplayName).ToArray();
        }, cancellationToken);
    }

    public async Task<long?> CalculateSizeAsync(MinecraftWorld world, CancellationToken cancellationToken = default)
    {
        if (world.IsLinked) { world.SizeUnavailable = true; return null; }
        await _sizeQueue.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                long total = 0;
                var pending = new Stack<string>(); pending.Push(world.FolderPath);
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var current = pending.Pop();
                    foreach (var file in Directory.EnumerateFiles(current))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var info = new FileInfo(file);
                        if (!info.Attributes.HasFlag(System.IO.FileAttributes.ReparsePoint)) total += info.Length;
                    }
                    foreach (var child in Directory.EnumerateDirectories(current))
                    {
                        var info = new DirectoryInfo(child);
                        if (!info.Attributes.HasFlag(System.IO.FileAttributes.ReparsePoint)) pending.Push(child);
                    }
                }
                (dispatcher ?? new Emerald.CoreX.Services.InlineUiDispatcher()).Invoke(() => world.SizeBytes = total);
                return (long?)total;
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.LogDebug(ex, "Could not calculate world size."); (dispatcher ?? new Emerald.CoreX.Services.InlineUiDispatcher()).Invoke(() => world.SizeUnavailable = true); return null; }
        finally { _sizeQueue.Release(); }
    }

    private MinecraftWorld ParseWorld(DirectoryInfo directory, string primary, string backup, bool linked)
    {
        Exception? primaryError = null;
        if (File.Exists(primary))
        {
            try { return ReadMetadata(directory, primary, false, linked); }
            catch (Exception ex) when (IsMetadataFailure(ex)) { primaryError = ex; }
        }
        if (File.Exists(backup))
        {
            try { return ReadMetadata(directory, backup, true, linked); }
            catch (Exception ex) when (IsMetadataFailure(ex)) { primaryError ??= ex; }
        }
        logger.LogWarning(primaryError, "Could not read world metadata in {WorldFolder}.", directory.Name);
        return new() { FolderName = directory.Name, FolderPath = directory.FullName, DisplayName = directory.Name, IconPath = ExistingIcon(directory), IsReadable = false, IsLinked = linked, Warning = "World metadata is unreadable." };
    }

    private static MinecraftWorld ReadMetadata(DirectoryInfo directory, string path, bool backup, bool linked)
    {
        var length = new FileInfo(path).Length;
        if (length is <= 0 or > MaxMetadataBytes) throw new InvalidDataException("World metadata exceeds the safe input limit.");
        var file = new NbtFile(new NbtOptions { Flavor = NbtFlavor.Java, ValidateOnRead = true, MaxAllocation = MaxMetadataBytes });
        file.LoadFromFile(path);
        var data = file.RootTag.Get<NbtCompound>("Data") ?? throw new InvalidDataException("Missing Data compound.");
        var version = data.Get<NbtCompound>("Version");
        var seed = data.Get<NbtCompound>("WorldGenSettings")?.Get<NbtLong>("seed")?.Value ?? data.Get<NbtLong>("RandomSeed")?.Value;
        var lastPlayedValue = data.Get<NbtLong>("LastPlayed")?.Value;
        return new()
        {
            FolderName = directory.Name,
            FolderPath = directory.FullName,
            DisplayName = data.Get<NbtString>("LevelName")?.Value ?? directory.Name,
            LastPlayed = lastPlayedValue is > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedValue.Value) : null,
            GameMode = ToMode(data.Get<NbtInt>("GameType")?.Value),
            Difficulty = data.Get<NbtByte>("Difficulty")?.Value,
            Hardcore = data.Get<NbtByte>("hardcore")?.Value is > 0,
            CheatsEnabled = data.Get<NbtByte>("allowCommands")?.Value is > 0,
            DataVersion = data.Get<NbtInt>("DataVersion")?.Value,
            MinecraftVersion = version?.Get<NbtString>("Name")?.Value,
            Seed = seed,
            IconPath = ExistingIcon(directory),
            UsedBackupMetadata = backup,
            IsReadable = true,
            IsLinked = linked,
            Warning = linked ? "This world folder is linked; size traversal is disabled." : backup ? "Recovered metadata from level.dat_old." : null
        };
    }

    private static MinecraftWorldGameMode ToMode(int? mode) => mode switch { 0 => MinecraftWorldGameMode.Survival, 1 => MinecraftWorldGameMode.Creative, 2 => MinecraftWorldGameMode.Adventure, 3 => MinecraftWorldGameMode.Spectator, _ => MinecraftWorldGameMode.Unknown };
    private static string? ExistingIcon(DirectoryInfo directory) { var path = Path.Combine(directory.FullName, "icon.png"); return File.Exists(path) ? path : null; }
    private static bool IsContained(string parent, string candidate) { var root = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar; return candidate.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal); }
    private static bool IsMetadataFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidDataException or NbtFormatException or EndOfStreamException;
}
