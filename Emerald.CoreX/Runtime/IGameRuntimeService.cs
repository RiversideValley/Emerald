using System.Collections.ObjectModel;
using Emerald.CoreX.Models;

namespace Emerald.CoreX.Runtime;

/// <summary>
/// Coordinates launching, tracking, and stopping active game sessions.
/// </summary>
public interface IGameRuntimeService
{
    /// <summary>
    /// Gets the currently known sessions in newest-first order.
    /// </summary>
    ObservableCollection<GameSession> Sessions { get; }

    /// <summary>
    /// Launches the supplied game and begins tracking its runtime session.
    /// </summary>
    Task<GameSession?> LaunchAsync(Game game, EAccount? account = null);

    /// <summary>
    /// Requests that the supplied game stop using the specified mode.
    /// </summary>
    Task StopAsync(Game game, GameStopMode mode);

    /// <summary>
    /// Returns the active session for the supplied game, if one exists.
    /// </summary>
    GameSession? TryGetActiveSession(Game game);

    /// <summary>
    /// Finds the most recent session for the supplied game path.
    /// </summary>
    GameSession? FindLatestSession(string gamePath);
}
