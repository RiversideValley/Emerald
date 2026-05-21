namespace Emerald.Services;

public interface IBaseSettingsService
{
    void Set<T>(string key, T value, string? headerComment = null);

    T Get<T>(string key, T defaultVal);

    bool Exists(string key);

    void Delete(string key);
}
