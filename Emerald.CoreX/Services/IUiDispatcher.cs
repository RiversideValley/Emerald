namespace Emerald.CoreX.Services;

internal interface IUiDispatcher
{
    bool HasThreadAccess { get; }

    void Invoke(Action action);

    Task InvokeAsync(Action action);
}

internal sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool HasThreadAccess => true;

    public void Invoke(Action action) => action();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
