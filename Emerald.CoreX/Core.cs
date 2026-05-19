using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Utils;
using CmlLib.Core.VersionMetadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Store;
using Emerald.Services;
using Microsoft.Extensions.Logging;
namespace Emerald.CoreX;

public sealed class SavedGame
{
    public string Path { get; set; } = string.Empty;

    public Versions.Version Version { get; set; } = new();

    public bool UsesCustomGameSettings { get; set; }

    public Models.GameSettings? CustomGameSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Models.GameSettings? GameOptions { get; set; }

    public Game ToGame(IGlobalGameSettingsService globalGameSettingsService, string? sharedMinecraftBasePath = null)
        => new(
            new MinecraftPath(Path),
            Version,
            UsesCustomGameSettings || GameOptions != null,
            CustomGameSettings ?? GameOptions,
            sharedMinecraftBasePath,
            globalGameSettingsService);

    public static SavedGame FromGame(Game game)
        => new()
        {
            Path = game.Path.BasePath,
            Version = game.Version,
            UsesCustomGameSettings = game.UsesCustomGameSettings,
            CustomGameSettings = game.UsesCustomGameSettings
                ? game.CustomGameSettings?.Clone()
                : null
        };
}

public sealed class SavedGameCollection
{
    public string BasePath { get; set; } = string.Empty;

    public SavedGame[] Games { get; set; } = [];
}

public partial class Core(
    ILogger<Core> _logger,
    INotificationService _notify,
    IBaseSettingsService settingsService,
    IMinecraftBaseSettingsService minecraftBaseSettingsService,
    IGameRuntimeService runtimeService,
    IGlobalGameSettingsService globalGameSettingsService,
    IStoreInstallRecordRepository storeInstallRecordRepository,
    IStoreSharedContentSettingsService storeSharedContentSettingsService) : ObservableObject
{
    public const string GamesFolderName = "Instances";
    public MinecraftLauncher Launcher { get; set; }
    public IGlobalGameSettingsService GlobalGameSettingsService => globalGameSettingsService;

    public event EventHandler? VersionsRefreshed;

    public bool IsRunning { get; set; } = false;
    public MinecraftPath? BasePath { get; private set; } = null;
    [ObservableProperty]
    private bool _isOfflineMode = false;

    public readonly ObservableCollection<Versions.Version> VanillaVersions = new();

    public readonly ObservableCollection<Game> Games = new();

    [ObservableProperty]
    private bool _initialized = false;

    [ObservableProperty]
    private bool _isRefreshing = false;

    public Models.GameSettings GameOptions => globalGameSettingsService.Settings;

    public void LoadGames()
    {
        if (BasePath == null)
        {
            _logger.LogWarning("Cannot load games, BasePath is not set");
            throw new InvalidOperationException("Cannot load games, BasePath is not set");
        }

        var gamesFolder = Path.Combine(BasePath.BasePath, GamesFolderName);
        if (!Path.Exists(gamesFolder))
        {
            _logger.LogInformation("Games folder does not exist, creating...");
            Directory.CreateDirectory(gamesFolder);
        }

        PrepareBaseScopedServices();
        var savedGames = LoadOrMigrateSavedGames();
        Games.Clear();
        if (savedGames.Length == 0)
        {
            _logger.LogInformation("Saved games paths does not contain any games");
            return;
        }

        foreach (var sg in savedGames)
        {
            try
            {
                Games.Add(sg.ToGame(globalGameSettingsService, BasePath.BasePath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load game from {dir}: {ex}", sg.Path, ex.Message);
                _notify.Error("FailedToLoadGame", $"Failed to load game from {sg.Path}", ex: ex);
            }
        }

        _logger.LogInformation("Loaded {count} games from", Games.Count);
    }
    public void SaveGames()
    {
        _logger.LogInformation("Saving {count} games", Games.Count);

        var toSave = Games.Select(x =>
            SavedGame.FromGame(x)
        ).ToArray();

        try
        {
            PrepareBaseScopedServices();
            minecraftBaseSettingsService.Set(SettingsKeys.SavedGames, toSave);

            _logger.LogInformation("Saved {count} games", toSave.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save games");
            throw;
        }
    }

    private void PrepareBaseScopedServices()
    {
        if (BasePath == null)
        {
            throw new InvalidOperationException("Cannot initialize base-scoped settings, BasePath is not set");
        }

        minecraftBaseSettingsService.UseBasePath(BasePath.BasePath);
        globalGameSettingsService.LoadForBasePath(BasePath.BasePath);
        storeInstallRecordRepository.LoadForBasePath(BasePath.BasePath);
        storeSharedContentSettingsService.LoadForBasePath(BasePath.BasePath);
    }

    private SavedGame[] LoadOrMigrateSavedGames()
    {
        if (BasePath == null)
        {
            throw new InvalidOperationException("Cannot load games, BasePath is not set");
        }

        if (minecraftBaseSettingsService.Exists(SettingsKeys.SavedGames))
        {
            return minecraftBaseSettingsService.Get<SavedGame[]>(SettingsKeys.SavedGames, []);
        }

        if (settingsService.Exists(SettingsKeys.SavedGames))
        {
            var savedCollections = settingsService.Get<SavedGameCollection[]>(SettingsKeys.SavedGames, []);
            var collection = savedCollections.FirstOrDefault(existing =>
                PathsEqual(existing.BasePath, BasePath.BasePath));

            if (collection != null)
            {
                minecraftBaseSettingsService.Set(SettingsKeys.SavedGames, collection.Games);

                var remaining = savedCollections
                    .Where(existing => !PathsEqual(existing.BasePath, BasePath.BasePath))
                    .ToArray();
                if (remaining.Length == 0)
                {
                    settingsService.Delete(SettingsKeys.SavedGames);
                }
                else
                {
                    settingsService.Set(SettingsKeys.SavedGames, remaining);
                }

                return collection.Games;
            }
        }

        return minecraftBaseSettingsService.Get<SavedGame[]>(SettingsKeys.SavedGames, []);
    }

    /// <summary>
    /// Initializes the Core with the given Minecraft path and retrieves the list of available vanilla Minecraft versions.
    /// </summary>
    /// <param name="basePath">The base path for Minecraft files. If null, initialization will require a previously set path.</param>
    /// <returns>A task that represents the asynchronous operation of initialization and refreshing Minecraft versions.</returns>
    public async Task InitializeAndRefresh(MinecraftPath? basePath = null)
    {
        var not = _notify.Create(
            "InitializingCore",
            isIndeterminate: true, 
            isCancellable: true
        );
        IsRefreshing = true;
        try
        {
            _logger.LogInformation("Trying to load vanilla minecraft versions from servers");

            if (!Initialized && basePath == null)
            {
                _logger.LogInformation("Minecraft Path must be set on first initialize");
                throw new InvalidOperationException("Minecraft Path must be set on first initialize");
            }
            if (basePath != null)
            {
                Launcher = new MinecraftLauncher(basePath);
                BasePath = basePath;
            }

            PrepareBaseScopedServices();
            LoadGames();
            Initialized = true;

            var l = await Launcher.GetAllVersionsAsync(not.CancellationToken.Value);

            VanillaVersions.Clear();
            VanillaVersions.AddRange(l.Select(x => new Versions.Version() { ReleaseTime = x.ReleaseTime.DateTime, BasedOn = x.Name, ReleaseType = x.Type }));
            IsOfflineMode = false;
            _notify.Complete(not.Id, true);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to load vanilla Minecraft versions; continuing in offline mode.");
            IsOfflineMode = true;
            _notify.Complete(not.Id, true, "OfflineMode");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to load vanilla minecraft versions: {ex}", ex.Message);
            _notify.Complete(not.Id, false, ex.Message, ex);
            Initialized = false;
        }
        finally
        {
            foreach (var game in Games)
            {
                game.CreateMCLauncher(IsOfflineMode);
            }
            _logger.LogInformation("Loaded {count} vanilla versions", VanillaVersions.Count);
            IsRefreshing = false;
            VersionsRefreshed?.Invoke(this, new());
        }
    }

    /// <summary>
    /// Installs the specified game version with optional file progress display.
    /// </summary>
    /// <param name="version">The version of the game to be installed. Must exist in the collection of games.</param>
    /// <param name="showFileprog">Specifies whether to display file progress during installation.</param>
    /// <returns>A task that represents the asynchronous operation of installing the game version.</returns>
    public async Task InstallGame(Game game, bool showFileprog = false)
    {
        try
        {
            await InstallGameOrThrow(game, showFileprog);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to install game {version}: {ex}", game?.Version.BasedOn, ex.Message);
            _notify.Error("GameInstallError", ex.Message, ex: ex);
        }
    }

    public async Task InstallGameOrThrow(Game game, bool showFileprog = false)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        var version = game.Version;
        _logger.LogInformation("Installing game {version}", version.BasedOn);

        await game.InstallVersionOrThrow(
            isOffline: IsOfflineMode,
            showFileProgress: showFileprog
        );

        SaveGames();
    }

    public Game CreateGame(Versions.Version version, string? folderName = null)
    {
        _logger.LogInformation("Adding game {version}", version.BasedOn);

        if (BasePath == null)
        {
            throw new InvalidOperationException("Cannot add a game before the base path is initialized.");
        }

        var resolvedFolderName = string.IsNullOrWhiteSpace(folderName)
            ? version.DisplayName
            : folderName.Trim();
        var path = Path.Combine(BasePath.BasePath, GamesFolderName, resolvedFolderName);

        var game = new Game(new(path), version, sharedMinecraftBasePath: BasePath.BasePath, globalGameSettingsService: globalGameSettingsService);

        Games.Add(game);
        try
        {
            SaveGames();
        }
        catch
        {
            Games.Remove(game);
            throw;
        }

        _notify.Info(
            "AddedGame",
            $"{version.DisplayName} based on {version.BasedOn} {version.Type}"
        );

        return game;
    }

    public void AddGame(Versions.Version version, string? folderName = null)
    {
        try
        {
            CreateGame(version, folderName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to add game {version}: {ex}", version.BasedOn, ex.Message);
            _notify.Error(
                "FailedToAddGame",
                $"Failed to add game {version.DisplayName} based on {version.BasedOn} {version.Type}",
               ex: ex
            );
        }
    }

    public void RemoveGame(Game game, bool deleteFolder = false)
    {
        try
        {
            _logger.LogInformation("Removing game {version}", game.Version.BasedOn);
            if (!Games.Contains(game))
            {
                _logger.LogWarning("Game {version} not found in collection", game.Version.BasedOn);
                throw new NullReferenceException($"Game {game.Version.BasedOn} not found in collection");
            }

            if (runtimeService.TryGetActiveSession(game) != null)
            {
                _logger.LogWarning("Refusing to remove running game {version}", game.Version.BasedOn);
                _notify.Warning("GameStillRunning", $"{game.Version.DisplayName} is still running. Stop it before removing the game.");
                return;
            }

            Games.Remove(game);
            SaveGames();

            if (deleteFolder && Path.Exists(game.Path.BasePath))
            {
                _logger.LogInformation("Deleting game folder {path}", game.Path.BasePath);
                Directory.Delete(game.Path.BasePath, true);
            }

            var not = _notify.Info(
                "RemovedGame",
                $"{game.Version.DisplayName} based on {game.Version.BasedOn} {game.Version.Type}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to remove game {version}: {ex}", game.Version.BasedOn, ex.Message);
            _notify.Error(
                "FailedToRemoveGame",
                $"Failed to remove game {game.Version.DisplayName} based on {game.Version.BasedOn} {game.Version.Type}",
               ex: ex
            );
        }
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
