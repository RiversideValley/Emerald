namespace Emerald.Services;

public interface IBaseSettingsService
{
    void Set<T>(string key, T value);

    T Get<T>(string key, T defaultVal);
}
