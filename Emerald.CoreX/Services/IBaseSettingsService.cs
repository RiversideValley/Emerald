using Microsoft.Extensions.Logging;
using System;

namespace Emerald.Services;

public interface IBaseSettingsService
{
    event EventHandler<string>? APINoMatch;

    void Set<T>(string key, T value, bool storeInFile = false);

    T Get<T>(string key, T defaultVal, bool loadFromFile = false);
}
