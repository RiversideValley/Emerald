using Emerald.CoreX.Services.Auth.OAuth;

namespace Emerald.CoreX.Services.Auth.ElyBy;

internal interface IElyByAuthClient
{
    BrowserOAuthAuthorizationRequest CreateOAuthAuthorizationRequest(string state, string? loginHint = null);

    Task<ElyByAuthSession> ExchangeOAuthCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<ElyByAuthSession> AuthenticateAsync(
        string login,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateAsync(
        string accessToken,
        string clientToken,
        CancellationToken cancellationToken = default);

    Task<ElyByAuthSession> RefreshAsync(
        ElyByStoredAccount account,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        ElyByStoredAccount account,
        CancellationToken cancellationToken = default);
}

internal sealed record ElyByAuthSession(
    string Name,
    string UUID,
    string AccessToken,
    string ClientToken,
    string? RefreshToken = null,
    DateTimeOffset? AccessTokenExpiresAt = null,
    ElyByAuthFlow AuthFlow = ElyByAuthFlow.Direct);
