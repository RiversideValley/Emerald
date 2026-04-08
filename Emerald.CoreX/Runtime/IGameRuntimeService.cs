using System.Collections.ObjectModel;

namespace Emerald.CoreX.Runtime;

public interface IGameRuntimeService
{
    ObservableCollection<GameSession> Sessions { get; }

    Task<GameSession?> LaunchAsync(Game game);

    Task StopAsync(Game game, GameStopMode mode);

    GameSession? TryGetActiveSession(Game game);

    GameSession? FindLatestSession(string gamePath);
}
