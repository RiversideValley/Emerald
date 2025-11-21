using Microsoft.Extensions.Logging;
using System;

namespace Emerald.Services;

public interface IBaseSettingsService
{
    event EventHandler<string>? APINoMatch;

    void Set<T>(string key, T value);

    T Get<T>(string key, T defaultVal);
}
