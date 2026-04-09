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

    public event EventHandler<string>? APINoMatch;

    public BaseSettingsService(ILogger<BaseSettingsService> logger)
    {
        _logger = logger;
    }

    public void Set<T>(string key, T value, bool storeInFile = false)
    {
        try
        {
            if (storeInFile)
            {
                SaveToFileAsync(key, value).Wait();
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
                try { SaveToFileAsync(key, value).Wait(); }
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
                return LoadFromFileAsync(key, defaultVal).GetAwaiter().GetResult();
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
                try { return LoadFromFileAsync(key, defaultVal).GetAwaiter().GetResult(); }
                catch (Exception fileEx) { _logger.LogError(fileEx, "Fallback file load failed for '{Key}'", key); }
            }
        }

        // if all else fails, persist default so next time there's a valid value
        Set(key, defaultVal, storeInFile: loadFromFile);
        return defaultVal;
    }

    private async Task SaveToFileAsync<T>(string key, T value)
    {
        string fileName = $"{key}.json";
        StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
            fileName, CreationCollisionOption.ReplaceExisting);

        string json = JsonSerializer.Serialize(value, _jsonOptions);
        await FileIO.WriteTextAsync(file, json);
    }

    private async Task<T> LoadFromFileAsync<T>(string key, T defaultVal)
    {
        try
        {
            string fileName = $"{key}.json";
            StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
            string json = await FileIO.ReadTextAsync(file);
            return JsonSerializer.Deserialize<T>(json) ?? defaultVal;
        }
        catch (FileNotFoundException)
        {
            return defaultVal;
        }
    }
}
