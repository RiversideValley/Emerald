using Microsoft.Extensions.Logging;
using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
using Emerald.CoreX.Notifications;
using System.Collections.ObjectModel;
using Emerald.CoreX.Helpers;
using Emerald.Services;
namespace Emerald.CoreX;

public class Core(ILogger<Core> _logger, INotificationService _notify, BaseSettingsService settingsService)
{
    public MinecraftLauncher Launcher { get; set; }

    public bool IsRunning { get; set; } = false;
    public MinecraftPath? BasePath { get; private set; } = null;
    public bool IsOfflineMode { get; private set; } = false;

    public readonly ObservableCollection<Versions.Version> VanillaVersions = new();

    public readonly ObservableCollection<Game> Games = new();

    private bool initialized = false;

    public Models.GameSettings GameOptions = new();
    public async Task Refresh()
    {

    }
    public async Task InitializeAndRefresh(MinecraftPath? basePath = null)
    {
        var not = _notify.Create(
            "InitializingCore",
            isIndeterminate: true, 
            isCancellable: true
        );
        try
        {
            GameOptions = settingsService.Get("BaseGameOptions", Models.GameSettings.FromMLaunchOption(new()));
            _logger.LogInformation("Trying to load vanilla minecraft versions from servers");

            if (!initialized && basePath == null)
            {
                _logger.LogInformation("Minecraft Path must be set on first initialize");
                throw new InvalidOperationException("Minecraft Path must be set on first initialize");
            }
            if (basePath != null)
            {
                Launcher = new MinecraftLauncher(basePath);
                BasePath = basePath;
            }

            initialized = true;

            var l = await Launcher.GetAllVersionsAsync(not.CancellationToken.Value);

            VanillaVersions.Clear();
            VanillaVersions.AddRange(l.Select(x => new Versions.Version() { Metadata = x, BasedOn = x.Name, ReleaseType = x.Type }));
            IsOfflineMode = false;
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
            initialized = false;
        }
        _logger.LogInformation("Loaded {count} vanilla versions", VanillaVersions.Count);
    }

    public async Task InstallGame(Versions.Version version, bool showFileprog = false)
    {
        
        try
        {
            _logger.LogInformation("Installing game {version}", version.BasedOn);

            var game =  Games.Where(x => x.Equals(version)).First();

            if(game == null)
            {
                _logger.LogWarning("Game {version} not found", version.BasedOn);
                throw new NullReferenceException($"Game {version.BasedOn} not found");
            }

            await game.InstallVersion(
                isOffline: IsOfflineMode,
                showFileProgress: showFileprog
            );

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

            var path = Path.Combine( BasePath.BasePath, version.DisplayName);


            var game = new Game(new(path), GameOptions);
            Games.Add(game);

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
}
