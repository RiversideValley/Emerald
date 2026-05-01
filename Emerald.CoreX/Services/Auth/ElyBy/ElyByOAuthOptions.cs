namespace Emerald.CoreX.Services.Auth.ElyBy;

internal sealed record ElyByOAuthOptions(
    string ClientId,
    string ClientSecret,
    string RedirectUri,
    string Scope = ElyByOAuthOptions.DefaultScope)
{
    public const string DefaultScope = "account_info offline_access minecraft_server_session";

    public bool IsConfigured
        => !IsPlaceholder(ClientId)
           && !IsPlaceholder(ClientSecret)
           && Uri.TryCreate(RedirectUri, UriKind.Absolute, out _);

    private static bool IsPlaceholder(string? value)
        => string.IsNullOrWhiteSpace(value)
           || value.Contains("TODO", StringComparison.OrdinalIgnoreCase)
           || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}
