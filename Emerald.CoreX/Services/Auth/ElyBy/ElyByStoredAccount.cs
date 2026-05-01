namespace Emerald.CoreX.Services.Auth.ElyBy;

internal sealed class ElyByStoredAccount
{
    public string UniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UUID { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ClientToken { get; set; } = string.Empty;
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
}
