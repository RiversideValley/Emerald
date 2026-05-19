using Emerald.CoreX.Helpers;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Store;

public interface IStoreSharedContentSettingsService
{
    StoreSharedContentSettings Settings { get; }

    StoreLinkMode GetPreferredLinkMode();

    void Save();
}

public sealed class StoreSharedContentSettingsService : IStoreSharedContentSettingsService
{
    private readonly IBaseSettingsService _baseSettingsService;
    private readonly ILogger<StoreSharedContentSettingsService> _logger;

    public StoreSharedContentSettings Settings { get; }

    public StoreSharedContentSettingsService(
        IBaseSettingsService baseSettingsService,
        ILogger<StoreSharedContentSettingsService> logger)
    {
        _baseSettingsService = baseSettingsService;
        _logger = logger;
        Settings = _baseSettingsService.Get(SettingsKeys.StoreSharedContentSettings, new StoreSharedContentSettings());
    }

    public StoreLinkMode GetPreferredLinkMode()
        => OperatingSystem.IsWindows()
            ? Settings.WindowsLinkMode
            : Settings.UnixLinkMode;

    public void Save()
    {
        _baseSettingsService.Set(SettingsKeys.StoreSharedContentSettings, Settings);
        _logger.LogDebug("Saved shared store content settings.");
    }
}
