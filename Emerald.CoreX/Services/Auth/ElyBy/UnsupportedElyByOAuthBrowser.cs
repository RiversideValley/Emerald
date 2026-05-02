namespace Emerald.CoreX.Services.Auth.ElyBy;

internal sealed class UnsupportedElyByOAuthBrowser : IElyByOAuthBrowser
{
    public Task<ElyByOAuthAuthorizationResult> AuthorizeAsync(
        ElyByOAuthAuthorizationRequest request,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Ely.by browser authentication is not available in this environment.");
}
