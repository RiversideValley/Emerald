using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Emerald.Services;

public class BaseSettingsService : IBaseSettingsService
{
    private readonly ILogger<BaseSettingsService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFolder;

    public event EventHandler<string>? APINoMatch;

    public BaseSettingsService(ILogger<BaseSettingsService> logger)
    {
        _logger = logger;

        // Use the LocalFolder path as the base folder for file-based settings
        _settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald");
    }

    public void Set<T>(string key, T value, bool storeInFile = false)
    {
        try
        {
            if (storeInFile)
            {
                SaveToFile(key, value);
            }
            else
            {
                string json = JsonSerializer.Serialize(value, _jsonOptions);
                ApplicationData.Current.LocalSettings.Values[key] = json;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving key '{Key}'", key);

            // fallback: if in-memory save failed, try file once
            if (!storeInFile)
            {
                try { SaveToFile(key, value); }
                catch (Exception fileEx) { _logger.LogError(fileEx, "Fallback file save failed for '{Key}'", key); }
            }
        }
    }

    public T Get<T>(string key, T defaultVal, bool loadFromFile = false)
    {
        try
        {
            if (loadFromFile)
            {
                return LoadFromFile(key, defaultVal);
            }
            else if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object? value)
                     && value is string json
                     && !string.IsNullOrWhiteSpace(json))
            {
                return JsonSerializer.Deserialize<T>(json) ?? defaultVal;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading key '{Key}'", key);

            // fallback: if in-memory load failed, try file once
            if (!loadFromFile)
            {
                try { return LoadFromFile(key, defaultVal); }
                catch (Exception fileEx) { _logger.LogError(fileEx, "Fallback file load failed for '{Key}'", key); }
            }
        }

        // if all else fails, persist default so next time there's a valid value
        Set(key, defaultVal, storeInFile: loadFromFile);
        return defaultVal;
    }

    private void SaveToFile<T>(string key, T value)
    {
        string filePath = Path.Combine(_settingsFolder, $"{key}.json");
        string json = JsonSerializer.Serialize(value, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    private T LoadFromFile<T>(string key, T defaultVal)
    {
        string filePath = Path.Combine(_settingsFolder, $"{key}.json");

        if (!File.Exists(filePath))
            return defaultVal;

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json) ?? defaultVal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading key '{Key}' from file storage", key);
            return defaultVal;
        }
    }
}
