using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Emerald.Services;

public class BaseSettingsService : IBaseSettingsService
{
    private readonly ILogger<BaseSettingsService> _logger;
    private readonly string? _defaultHeaderComment;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly string _settingsFolder;

    public BaseSettingsService(
        ILogger<BaseSettingsService> logger,
        string? settingsFolderPath = null,
        string? defaultHeaderComment = null)
    {
        _logger = logger;
        _defaultHeaderComment = defaultHeaderComment;

        // Use the LocalFolder path as the base folder for file-based settings
        _settingsFolder =  settingsFolderPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald", "Settings");

        // Ensure the directory exists immediately
        if (!Directory.Exists(_settingsFolder))
        {
            Directory.CreateDirectory(_settingsFolder);
        }
    }

    public void Set<T>(string key, T value, string? headerComment = null)
    {
        try
        {
            SaveToFile(key, value, headerComment ?? _defaultHeaderComment);
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
                _logger.LogCritical(writeEx, "Could not write default value for '{Key}' after load failure.", key);
            }

            return defaultVal;
        }
    }

    public bool Exists(string key)
        => File.Exists(GetFilePath(key));

    public void Delete(string key)
    {
        try
        {
            var filePath = GetFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting key '{Key}' from file.", key);
        }
    }

    private void SaveToFile<T>(string key, T value, string? headerComment)
    {
        var filePath = GetFilePath(key);
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        if (!string.IsNullOrWhiteSpace(headerComment))
        {
            json = $"{FormatHeaderComment(headerComment)}{Environment.NewLine}{json}";
        }

        File.WriteAllText(filePath, json);
    }

    private T LoadFromFile<T>(string key, T defaultVal)
    {
        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            // If the file doesn't exist, create it with the default value immediately
            Set(key, defaultVal);
            return defaultVal;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? defaultVal;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Corrupted JSON for key '{Key}'. Returning default.", key);
            return defaultVal;
        }
    }

    private string GetFilePath(string key)
        => Path.Combine(_settingsFolder, $"{key}.json");

    private static string FormatHeaderComment(string headerComment)
    {
        var trimmed = headerComment.TrimEnd();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            ? trimmed
            : $"// {trimmed}";
    }
}
