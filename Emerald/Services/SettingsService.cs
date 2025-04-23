using Emerald.Helpers.Settings;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Emerald.Services;
public class SettingsService(BaseSettingsService _baseService, ILogger<SettingsService> _logger)
{
    private readonly ILogger<SettingsService> _logger;

    public Helpers.Settings.JSON.Settings Settings { get; private set; }
    public Helpers.Settings.JSON.Account[] Accounts { get; set; }

    public event EventHandler<string>? APINoMatch;


    public void LoadData()
    {
        try
        {
            _logger.LogInformation("Loading settings and accounts.");
            Settings = _baseService.GetSerializedFromSettings("Settings", Helpers.Settings.JSON.Settings.CreateNew());
            Accounts = _baseService.GetSerializedFromSettings("Accounts", Array.Empty<Helpers.Settings.JSON.Account>());

            if (Settings.APIVersion != DirectResoucres.SettingsAPIVersion)
            {
                _logger.LogWarning("API version mismatch. Triggering APINoMatch event.");
                APINoMatch?.Invoke(this, ApplicationData.Current.LocalSettings.Values["Settings"] as string);
                ApplicationData.Current.LocalSettings.Values["Settings"] = Helpers.Settings.JSON.Settings.CreateNew().Serialize();
                Settings = JsonSerializer.Deserialize<Helpers.Settings.JSON.Settings>(ApplicationData.Current.LocalSettings.Values["Settings"] as string);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings data.");
        }
    }
    public void SaveData()
    {
        try
        {
            Settings.LastSaved = DateTime.Now;
            ApplicationData.Current.LocalSettings.Values["Settings"] = JsonSerializer.Serialize(Settings);
            ApplicationData.Current.LocalSettings.Values["Accounts"] = JsonSerializer.Serialize(Accounts);

            _logger.LogInformation("Settings and accounts saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving data.");
        }
    }
}
