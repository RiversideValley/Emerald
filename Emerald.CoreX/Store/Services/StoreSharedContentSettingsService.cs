using Emerald.CoreX.Helpers;
using Emerald.CoreX.Services;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Store;

public interface IStoreSharedContentSettingsService
{
    StoreSharedContentSettings Settings { get; }

    void LoadForBasePath(string basePath);

    StoreLinkMode GetPreferredLinkMode();

    void Save();
}

public sealed class StoreSharedContentSettingsService : IStoreSharedContentSettingsService
{
    private readonly IBaseSettingsService _baseSettingsService;
    private readonly IMinecraftBaseSettingsService _minecraftBaseSettingsService;
    private readonly ILogger<StoreSharedContentSettingsService> _logger;

    public StoreSharedContentSettings Settings { get; }

    public StoreSharedContentSettingsService(
        IBaseSettingsService baseSettingsService,
        IMinecraftBaseSettingsService minecraftBaseSettingsService,
        ILogger<StoreSharedContentSettingsService> logger)
    {
        _baseSettingsService = baseSettingsService;
        _minecraftBaseSettingsService = minecraftBaseSettingsService;
        _logger = logger;
        Settings = _baseSettingsService.Exists(SettingsKeys.StoreSharedContentSettings)
            ? _baseSettingsService.Get(SettingsKeys.StoreSharedContentSettings, new StoreSharedContentSettings())
            : new StoreSharedContentSettings();
    }

    public void LoadForBasePath(string basePath)
    {
        _minecraftBaseSettingsService.UseBasePath(basePath);
        var loaded = LoadOrMigrateSettings();

        Settings.WindowsLinkMode = loaded.WindowsLinkMode;
        Settings.UnixLinkMode = loaded.UnixLinkMode;
    }

    public StoreLinkMode GetPreferredLinkMode()
        => OperatingSystem.IsWindows()
            ? Settings.WindowsLinkMode
            : Settings.UnixLinkMode;

    public void Save()
    {
        if (_minecraftBaseSettingsService.IsInitialized)
        {
            _minecraftBaseSettingsService.Set(SettingsKeys.StoreSharedContentSettings, Settings);
        }
        else
        {
            _baseSettingsService.Set(SettingsKeys.StoreSharedContentSettings, Settings);
        }

        _logger.LogDebug("Saved shared store content settings.");
    }

    private StoreSharedContentSettings LoadOrMigrateSettings()
    {
        if (_minecraftBaseSettingsService.Exists(SettingsKeys.StoreSharedContentSettings))
        {
            return _minecraftBaseSettingsService.Get(SettingsKeys.StoreSharedContentSettings, new StoreSharedContentSettings());
        }

        if (_baseSettingsService.Exists(SettingsKeys.StoreSharedContentSettings))
        {
            var migrated = _baseSettingsService.Get(SettingsKeys.StoreSharedContentSettings, new StoreSharedContentSettings());
            _minecraftBaseSettingsService.Set(SettingsKeys.StoreSharedContentSettings, migrated);
            _baseSettingsService.Delete(SettingsKeys.StoreSharedContentSettings);
            return migrated;
        }

        return _minecraftBaseSettingsService.Get(SettingsKeys.StoreSharedContentSettings, new StoreSharedContentSettings());
    }
}
