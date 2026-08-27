using Emerald.CoreX.Services.Auth.OAuth;
using Microsoft.UI.Dispatching;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Emerald.Services;

internal sealed class UnoSystemBrowserLauncher(DispatcherQueue dispatcherQueue) : ISystemBrowserLauncher
{
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (dispatcherQueue.HasThreadAccess)
            return Launcher.LaunchUriAsync(uri).AsTask(cancellationToken);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    completion.SetResult(await Launcher.LaunchUriAsync(uri));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("Failed to dispatch browser launch to the UI thread."));
        }

        return completion.Task.WaitAsync(cancellationToken);
    }
}
