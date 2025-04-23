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

    public Models.Game GameOptions = new();

    public async Task InitializeAndRefresh(MinecraftPath? basePath = null)
    {
        var not = _notify.Create(
            "InitializingCore",
            isIndeterminate: true
        );
        try
        {
            GameOptions = settingsService.Get("BaseGameOptions", new Models.Game());
            _logger.LogInformation("Trying to load vanilla minecraft versions from servers");

            Launcher = new MinecraftLauncher(basePath);
            var l = await Launcher.GetAllVersionsAsync();

            VanillaVersions.Clear();
            VanillaVersions.AddRange(l.Select(x => new Versions.Version() { Metadata = x, BasedOn = x.Name, ReleaseType = x.Type }));
            IsOfflineMode = false;
        }
        catch (System.Net.Sockets.SocketException)
        {
            IsOfflineMode = false;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to load vanilla minecraft versions: {ex}", ex.Message);
            throw;
        }
        finally
        {
            _logger.LogInformation("Loaded {count} vanilla versions", VanillaVersions.Count);
            initialized = true;
            _notify.Complete(not.Id, true);
        } 
    }

    public async Task AddGame(Versions.Version version)
    {
        _notify.Info(
            "AddedGame",
            $"{version.DisplayName} based on {version.BasedOn} {version.Type}"
        );
        try
        {
            _logger.LogInformation("Adding game {version}", version.BasedOn);

            var path = Path.Combine( BasePath.BasePath, version.DisplayName);


            var game = new Game(new(path), GameOptions);
            Games.Add(game);
            _notify.Complete(not.Id, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to add game {version}: {ex}", version.BasedOn, ex.Message);
            _notify.Complete(not.Id, false, ex.Message, ex);
        }
    }
}
