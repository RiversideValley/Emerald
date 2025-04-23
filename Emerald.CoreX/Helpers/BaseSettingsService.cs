using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Emerald.Services;

public class BaseSettingsService
{
    private readonly ILogger<BaseSettingsService> _logger;

    public event EventHandler<string>? APINoMatch;

    public BaseSettingsService(ILogger<BaseSettingsService> logger)
    {
        _logger = logger;
    }
    public void Set<T>(string key, T value)
    {
        try
        {
            string json = JsonSerializer.Serialize<T>(value);
            ApplicationData.Current.LocalSettings.Values[key] = json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing key '{Key}' from settings", key);
        }
    }

    public T Get<T>(string key, T defaultVal)
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
        Set(key, defaultVal);

        return defaultVal;
    }
}
