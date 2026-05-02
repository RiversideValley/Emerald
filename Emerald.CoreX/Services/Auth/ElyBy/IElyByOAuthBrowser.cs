namespace Emerald.CoreX.Services.Auth.ElyBy;

internal interface IElyByOAuthBrowser
{
    Task<ElyByOAuthAuthorizationResult> AuthorizeAsync(
        ElyByOAuthAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed record ElyByOAuthAuthorizationRequest(
    Uri AuthorizationUri,
    Uri RedirectUri,
    string State);

internal sealed record ElyByOAuthAuthorizationResult(string Code);
