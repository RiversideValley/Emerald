using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Utils;
using CmlLib.Core.VersionMetadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.Services;
using Microsoft.Extensions.Logging;
namespace Emerald.CoreX;

public record SavedGame(string Path, Versions.Version Version, Models.GameSettings GameOptions);

public record SavedGameCollection(string BasePath, SavedGame[] Games);

public partial class Core(ILogger<Core> _logger, INotificationService _notify, IBaseSettingsService settingsService) : ObservableObject
{
    public const string GamesFolderName = "EmeraldGames";
    public MinecraftLauncher Launcher { get; set; }

    public event EventHandler? VersionsRefreshed;

    public bool IsRunning { get; set; } = false;
    public MinecraftPath? BasePath { get; private set; } = null;
    public bool IsOfflineMode { get; private set; } = false;

    public readonly ObservableCollection<Versions.Version> VanillaVersions = new();

    public readonly ObservableCollection<Game> Games = new();

    [ObservableProperty]
    private bool _initialized = false;

    [ObservableProperty]
    private bool _isRefreshing = false;

    public Models.GameSettings GameOptions = new();

    private SavedGameCollection[] SavedgamesWithPaths = [];

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

        SavedgamesWithPaths = settingsService.Get<SavedGameCollection[]>(SettingsKeys.SavedGames, []);

        var collection = SavedgamesWithPaths.FirstOrDefault(x => x.BasePath == BasePath.BasePath);
        if (collection == null)
        {
            _logger.LogInformation("Saved games paths does not contain any games");
            return;
        }

        foreach (var sg in collection.Games)
        {
            try
            {
                var game = Game.FromTuple((sg.Path, sg.Version, sg.GameOptions));
                Games.Add(game);
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
            new SavedGame(x.Path.BasePath, x.Version, x.Options)
        ).ToArray();

        try
        {
            var list = SavedgamesWithPaths.ToList();
            list.RemoveAll(x => x.BasePath == BasePath.BasePath);
            list.Add(new SavedGameCollection(BasePath.BasePath, toSave));

            SavedgamesWithPaths = list.ToArray();
            settingsService.Set(SettingsKeys.SavedGames, SavedgamesWithPaths);

            _logger.LogInformation("Saved {count} games", toSave.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save games");
            throw;
        }
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
            GameOptions = settingsService.Get("BaseGameOptions", Models.GameSettings.FromMLaunchOption(new()));
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
            
            LoadGames();
            Initialized = true;

            var l = await Launcher.GetAllVersionsAsync(not.CancellationToken.Value);

            VanillaVersions.Clear();
            VanillaVersions.AddRange(l.Select(x => new Versions.Version() { ReleaseTime = x.ReleaseTime.DateTime, BasedOn = x.Name, ReleaseType = x.Type }));
            IsOfflineMode = false;
            _notify.Complete(not.Id, true);
        }
        catch (HttpRequestException)
        {
            IsOfflineMode = false;
            _notify.Complete(not.Id, true,"OfflineMode");
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
        var version = game.Version;

        try
        {
            _logger.LogInformation("Installing game {version}", version.BasedOn);

            if(game == null)
            {
                _logger.LogWarning("Game {version} not found", version.BasedOn);
                throw new NullReferenceException($"Game {version.BasedOn} not found");
            }

            await game.InstallVersion(
                isOffline: IsOfflineMode,
                showFileProgress: showFileprog
            );
            
            SaveGames();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to install game {version}: {ex}", version.BasedOn, ex.Message);
            _notify.Error("GameInstallError", ex.Message, ex:ex);
        }
    }
    public void AddGame(Versions.Version version)
    {
        try
        {
            _logger.LogInformation("Adding game {version}", version.BasedOn);

            var path = Path.Combine( BasePath.BasePath, GamesFolderName, version.DisplayName);

            var game = new Game(new(path), GameOptions, version);


            Games.Add(game);
            SaveGames();

            var not = _notify.Info(
                "AddedGame",
                $"{version.DisplayName} based on {version.BasedOn} {version.Type}"
            );
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
}
