using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using System.IO;

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
        _settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald", "Settings");

        // Ensure the directory exists immediately
        if (!Directory.Exists(_settingsFolder))
        {
            Directory.CreateDirectory(_settingsFolder);
        }
    }

    public void Set<T>(string key, T value)
    {
        try
        {
            SaveToFile(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving key '{Key}' to file.", key);
        }
    }

    public T Get<T>(string key, T defaultVal)
    {
        try
        {
            return LoadFromFile(key, defaultVal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading key '{Key}' from file.", key);

            // If load fails, try to persist the default so the file is corrected for next time
            try
            {
                Set(key, defaultVal);
            }
            catch (Exception writeEx)
            {
                _logger.LogError(writeEx, "Could not write default value for '{Key}' after load failure.", key);
            }

            return defaultVal;
        }
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
        {
            // If the file doesn't exist, create it with the default value immediately
            Set(key, defaultVal);
            return defaultVal;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json) ?? defaultVal;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Corrupted JSON for key '{Key}'. Returning default.", key);
            return defaultVal;
        }
    }
}
