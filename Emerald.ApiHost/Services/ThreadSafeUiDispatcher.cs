using Emerald.CoreX.Services;

namespace Emerald.ApiHost.Services;

/// <summary>
/// Serializes all ObservableCollection mutations through a dedicated STA-like
/// sequential scheduler so concurrent callers don't race on collection state.
/// </summary>
public sealed class ThreadSafeUiDispatcher : IUiDispatcher, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool HasThreadAccess => false;

    public void Invoke(Action action)
    {
        _gate.Wait();
        try { action(); }
        finally { _gate.Release(); }
    }

    public async Task InvokeAsync(Action action)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { action(); }
        finally { _gate.Release(); }
    }

    public void Dispose() => _gate.Dispose();
}
