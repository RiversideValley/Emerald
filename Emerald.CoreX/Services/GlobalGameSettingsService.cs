using System.ComponentModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed class GlobalGameSettingsService : IGlobalGameSettingsService
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(250);

    private readonly IBaseSettingsService _baseSettingsService;
    private readonly IMinecraftBaseSettingsService _minecraftBaseSettingsService;
    private readonly ILogger<GlobalGameSettingsService> _logger;
    private readonly object _saveGate = new();

    private CancellationTokenSource? _pendingSaveCts;
    private bool _suppressTracking;

    public GameSettings Settings { get; }

    public GlobalGameSettingsService(
        IBaseSettingsService baseSettingsService,
        IMinecraftBaseSettingsService minecraftBaseSettingsService,
        ILogger<GlobalGameSettingsService> logger)
    {
        _baseSettingsService = baseSettingsService;
        _minecraftBaseSettingsService = minecraftBaseSettingsService;
        _logger = logger;

        Settings = _baseSettingsService.Exists(SettingsKeys.BaseGameOptions)
            ? _baseSettingsService.Get(SettingsKeys.BaseGameOptions, GameSettings.FromMLaunchOption(new()))
            : GameSettings.FromMLaunchOption(new());
        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public GameSettings CloneCurrent()
        => Settings.Clone();

    public void LoadForBasePath(string basePath)
    {
        _minecraftBaseSettingsService.UseBasePath(basePath);
        var loaded = LoadOrMigrateSettings();

        try
        {
            _suppressTracking = true;
            Settings.ApplyFrom(loaded);
        }
        finally
        {
            _suppressTracking = false;
        }
    }

    public void Save()
    {
        try
        {
            _suppressTracking = true;
            if (_minecraftBaseSettingsService.IsInitialized)
            {
                _minecraftBaseSettingsService.Set(SettingsKeys.BaseGameOptions, Settings);
            }
            else
            {
                _baseSettingsService.Set(SettingsKeys.BaseGameOptions, Settings);
            }

            _logger.LogDebug("Saved global game settings.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save global game settings.");
            throw;
        }
        finally
        {
            _suppressTracking = false;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressTracking)
        {
            return;
        }

        QueueSave();
    }

    private void QueueSave()
    {
        CancellationTokenSource cts;

        lock (_saveGate)
        {
            _pendingSaveCts?.Cancel();
            _pendingSaveCts?.Dispose();
            _pendingSaveCts = new CancellationTokenSource();
            cts = _pendingSaveCts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounce, cts.Token);
                Save();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued save for global game settings failed.");
            }
        });
    }

    private GameSettings LoadOrMigrateSettings()
    {
        if (_minecraftBaseSettingsService.Exists(SettingsKeys.BaseGameOptions))
        {
            return _minecraftBaseSettingsService.Get(SettingsKeys.BaseGameOptions, GameSettings.FromMLaunchOption(new()));
        }

        if (_baseSettingsService.Exists(SettingsKeys.BaseGameOptions))
        {
            var migrated = _baseSettingsService.Get(SettingsKeys.BaseGameOptions, GameSettings.FromMLaunchOption(new()));
            _minecraftBaseSettingsService.Set(SettingsKeys.BaseGameOptions, migrated);
            _baseSettingsService.Delete(SettingsKeys.BaseGameOptions);
            return migrated;
        }

        return _minecraftBaseSettingsService.Get(SettingsKeys.BaseGameOptions, GameSettings.FromMLaunchOption(new()));
    }
}
