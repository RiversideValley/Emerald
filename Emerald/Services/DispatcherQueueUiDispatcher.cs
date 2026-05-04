using Emerald.CoreX.Services;
using Microsoft.UI.Dispatching;

namespace Emerald.Services;

internal sealed class DispatcherQueueUiDispatcher(DispatcherQueue dispatcherQueue) : IUiDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue = dispatcherQueue;

    public bool HasThreadAccess => _dispatcherQueue.HasThreadAccess;

    public void Invoke(Action action)
        => InvokeAsync(action).GetAwaiter().GetResult();

    public Task InvokeAsync(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("Failed to dispatch work to the UI thread."));
        }

        return completion.Task;
    }
}
