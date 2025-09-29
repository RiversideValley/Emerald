using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace Emerald.Services;

public class BaseSettingsService : IBaseSettingsService
{
    private readonly ILogger<BaseSettingsService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true // Makes file easier to read/debug
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
            _logger.LogError(ex, "Error saving key '{Key}' to settings.", key);
            if(ex.Message != "FileError")
            {
                _logger.LogInformation("Saving it to a file anyway to avoid loss");
                Set(key, value, storeInFile: true);
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
            else
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object? value)
                    && value is string json
                    && !string.IsNullOrWhiteSpace(json))
                {
                    return JsonSerializer.Deserialize<T>(json) ?? defaultVal;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing key '{Key}' from settings", key);
            if (ex.Message != "FileError")
            {
                _logger.LogInformation("Loading it from files anyway to avoid loss");
                Get(key, defaultVal, loadFromFile: true);
            }
        }

        // Save default value if deserialization fails or key is missing
        Set(key, defaultVal, storeInFile: loadFromFile);

        return defaultVal;
    }

    private async Task SaveToFileAsync<T>(string key, T value)
    {
        try
        {
            string fileName = $"{key}.json";
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                fileName, CreationCollisionOption.ReplaceExisting);

            string json = JsonSerializer.Serialize(value, _jsonOptions);
            await FileIO.WriteTextAsync(file, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing key '{Key}' to file storage", key);
            throw new Exception("FileError", ex);
        }
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
            // Return default if file doesn't exist
            return defaultVal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading key '{Key}' from file storage", key);
            throw new Exception("FileError", ex);
        }
    }
}
