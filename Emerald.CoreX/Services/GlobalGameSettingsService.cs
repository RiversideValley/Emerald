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
    private readonly ILogger<GlobalGameSettingsService> _logger;
    private readonly object _saveGate = new();

    private CancellationTokenSource? _pendingSaveCts;
    private bool _suppressTracking;

    public GameSettings Settings { get; }

    public GlobalGameSettingsService(IBaseSettingsService baseSettingsService, ILogger<GlobalGameSettingsService> logger)
    {
        _baseSettingsService = baseSettingsService;
        _logger = logger;

        Settings = _baseSettingsService.Get(SettingsKeys.BaseGameOptions, GameSettings.FromMLaunchOption(new()));
        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public GameSettings CloneCurrent()
        => Settings.Clone();

    public void Save()
    {
        try
        {
            _suppressTracking = true;
            _baseSettingsService.Set(SettingsKeys.BaseGameOptions, Settings);
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
}
