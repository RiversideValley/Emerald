using Emerald.Helpers.Settings;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Emerald.Services;
public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;

    public Helpers.Settings.JSON.Settings Settings { get; private set; }
    public Helpers.Settings.JSON.Account[] Accounts { get; set; }

    public event EventHandler<string>? APINoMatch;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
    }

    public T GetSerializedFromSettings<T>(string key, T def)
    {
        try
        {
            string json = ApplicationData.Current.LocalSettings.Values[key] as string;
            if (!string.IsNullOrWhiteSpace(json))
            {
                return JsonSerializer.Deserialize<T>(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing key '{Key}' from settings", key);
        }

        // Save default value if deserialization fails or key is missing
        try
        {
            string json = JsonSerializer.Serialize(def);
            ApplicationData.Current.LocalSettings.Values[key] = json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing default value for key '{Key}'", key);
        }

        return def;
    }

    public void LoadData()
    {
        try
        {
            _logger.LogInformation("Loading settings and accounts.");
            Settings = GetSerializedFromSettings("Settings", Helpers.Settings.JSON.Settings.CreateNew());
            Accounts = GetSerializedFromSettings("Accounts", Array.Empty<Helpers.Settings.JSON.Account>());

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

    public async Task CreateBackup(string system)
    {
        try
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var backups = string.IsNullOrWhiteSpace(json) ? new Helpers.Settings.JSON.Backups() : JsonSerializer.Deserialize<Helpers.Settings.JSON.Backups>(json);

            backups.AllBackups ??= Array.Empty<Helpers.Settings.JSON.SettingsBackup>();
            backups.AllBackups = backups.AllBackups.Append(new Helpers.Settings.JSON.SettingsBackup { Time = DateTime.Now, Backup = system }).ToArray();

            json = backups.Serialize();
            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);

            _logger.LogInformation("Backup created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup.");
        }
    }

    public async Task DeleteBackup(int index)
    {
        try
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var backups = string.IsNullOrWhiteSpace(json) ? new Helpers.Settings.JSON.Backups() : JsonSerializer.Deserialize<Helpers.Settings.JSON.Backups>(json);

            backups.AllBackups?.ToList().RemoveAt(index);
            json = backups.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);

            _logger.LogInformation("Backup at index {Index} deleted successfully.", index);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup at index {Index}.", index);
        }
    }

    public async Task<List<Helpers.Settings.JSON.SettingsBackup>> GetBackups()
    {
        try
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var backups = string.IsNullOrWhiteSpace(json) ? new Helpers.Settings.JSON.Backups() : JsonSerializer.Deserialize<Helpers.Settings.JSON.Backups>(json);

            _logger.LogInformation("Backups retrieved successfully.");
            return backups.AllBackups?.ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backups.");
            return [];
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
