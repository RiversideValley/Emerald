using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services;

public interface IGlobalGameSettingsService
{
    GameSettings Settings { get; }

    GameSettings CloneCurrent();

    void Save();
}
