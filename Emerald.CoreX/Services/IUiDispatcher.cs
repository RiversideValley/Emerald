namespace Emerald.CoreX.Services;

public interface IUiDispatcher
{
    bool HasThreadAccess { get; }

    void Invoke(Action action);

    Task InvokeAsync(Action action);
}

public sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool HasThreadAccess => true;

    public void Invoke(Action action) => action();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
