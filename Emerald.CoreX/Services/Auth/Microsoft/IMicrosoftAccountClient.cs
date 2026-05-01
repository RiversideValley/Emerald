using CmlLib.Core.Auth;

namespace Emerald.CoreX.Services.Auth.Microsoft;

internal interface IMicrosoftAccountClient
{
    Task InitializeAsync(string clientId, string accountStorePath);

    IReadOnlyList<MicrosoftAccountInfo> GetAccounts();

    string? GetDefaultAccountIdentifier();

    Task<MicrosoftInteractiveSignInResult> SignInInteractivelyAsync();

    Task<MSession> AuthenticateAsync(string accountIdentifier);

    Task SignOutAsync(string accountIdentifier);
}

internal sealed record MicrosoftAccountInfo(
    string Identifier,
    string Name,
    string UUID,
    DateTime LastAccess);

internal sealed record MicrosoftInteractiveSignInResult(
    string? Identifier,
    string? Username,
    string? UUID);
