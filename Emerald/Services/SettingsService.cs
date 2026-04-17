using System.ComponentModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.Helpers.Settings;
using Emerald.Models;
using Microsoft.Extensions.Logging;

namespace Emerald.Services;

public class SettingsService(
    IBaseSettingsService baseService,
    IGlobalGameSettingsService globalGameSettingsService,
    ILogger<SettingsService> logger)
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(250);

    private readonly object _saveGate = new();
    private readonly List<INotifyPropertyChanged> _trackedObjects = [];

    private CancellationTokenSource? _pendingSaveCts;
    private bool _suppressTracking;

    public Helpers.Settings.JSON.Settings Settings { get; private set; } = Helpers.Settings.JSON.Settings.CreateNew();

    public GameSettings GlobalGameSettings => globalGameSettingsService.Settings;

    public void LoadData()
    {
        try
        {
            logger.LogInformation("Loading settings from the file-backed settings store.");

            DetachTracking();

            var loadedSettings = baseService.Get(SettingsKeys.Settings, Helpers.Settings.JSON.Settings.CreateNew());
            if (loadedSettings.APIVersion != DirectResoucres.SettingsAPIVersion)
            {
                logger.LogWarning(
                    "Settings API version mismatch. Expected {ExpectedVersion}, found {FoundVersion}. Resetting settings.",
                    DirectResoucres.SettingsAPIVersion,
                    loadedSettings.APIVersion);

                loadedSettings = Helpers.Settings.JSON.Settings.CreateNew();
                baseService.Set(SettingsKeys.Settings, loadedSettings);
            }

            if (loadedSettings.App.Updates.PreferredChannel == AppReleaseChannel.Nightly
                && DirectResoucres.ReleaseChannel != AppReleaseChannel.Nightly)
            {
                loadedSettings.App.Updates.PreferredChannel = loadedSettings.App.Updates.IncludePreReleases
                    ? AppReleaseChannel.Prerelease
                    : DirectResoucres.ReleaseChannel;
            }

            Settings = loadedSettings;
            AttachTracking();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading settings data.");
            throw;
        }
    }

    public void SaveData()
    {
        try
        {
            _suppressTracking = true;
            Settings.LastSaved = DateTime.Now;
            baseService.Set(SettingsKeys.Settings, Settings);
            globalGameSettingsService.Save();
            logger.LogInformation("Settings saved successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving settings data.");
            throw;
        }
        finally
        {
            _suppressTracking = false;
        }
    }

    private void AttachTracking()
    {
        Track(Settings);
        Track(Settings.Minecraft);
        Track(Settings.Minecraft.Downloader);
        Track(Settings.Minecraft.MCVerionsConfiguration);
        Track(Settings.Minecraft.JVM);
        Track(Settings.App);
        Track(Settings.App.Appearance);
        Track(Settings.App.NewsFilter);
        Track(Settings.App.Store.Filter);
        Track(Settings.App.Store.SortOptions);
        Track(Settings.App.Updates);
    }

    private void DetachTracking()
    {
        foreach (var trackedObject in _trackedObjects)
        {
            trackedObject.PropertyChanged -= OnTrackedPropertyChanged;
        }

        _trackedObjects.Clear();
    }

    private void Track(INotifyPropertyChanged trackedObject)
    {
        trackedObject.PropertyChanged += OnTrackedPropertyChanged;
        _trackedObjects.Add(trackedObject);
    }

    private void OnTrackedPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
                SaveData();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Queued settings save failed.");
            }
        });
    }
}
