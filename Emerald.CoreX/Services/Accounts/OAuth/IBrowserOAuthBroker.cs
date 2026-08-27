namespace Emerald.CoreX.Services.Auth.OAuth;

internal interface IBrowserOAuthBroker
{
    Task<BrowserOAuthAuthorizationResult> AuthorizeAsync(
        BrowserOAuthAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

internal interface ISystemBrowserLauncher
{
    Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}

internal sealed record BrowserOAuthAuthorizationRequest(
    string ProviderDisplayName,
    Uri AuthorizationUri,
    Uri RedirectUri,
    string State);

internal sealed record BrowserOAuthAuthorizationResult(string Code);

internal enum BrowserOAuthFailureKind
{
    InvalidRedirectUri,
    CallbackPortUnavailable,
    BrowserLaunchFailed,
    TimedOut,
    StateMismatch,
    UserDenied,
    MalformedResponse
}

internal sealed class BrowserOAuthException(
    BrowserOAuthFailureKind failureKind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public BrowserOAuthFailureKind FailureKind { get; } = failureKind;
}
