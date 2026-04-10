namespace Emerald.CoreX.Store;

public sealed class StoreTagChip
{
    public StoreTagChip(string text, bool isMatch = false)
    {
        Text = text;
        IsMatch = isMatch;
    }

    public string Text { get; }
    public bool IsMatch { get; }
}
