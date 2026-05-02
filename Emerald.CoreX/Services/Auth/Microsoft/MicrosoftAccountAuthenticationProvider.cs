using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;

namespace Emerald.CoreX.Services.Auth.Microsoft;

internal sealed class MicrosoftAccountAuthenticationProvider(IMicrosoftAccountClient microsoftAccountClient) : IAccountAuthenticationProvider
{
    public AccountType AccountType => AccountType.Microsoft;
    public string ProviderId => AccountProviderIds.Microsoft;

    public async Task<GameAuthenticationResult> AuthenticateAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        var session = await microsoftAccountClient.AuthenticateAsync(account.UniqueId).ConfigureAwait(false);
        return new GameAuthenticationResult(session);
    }

    public Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
        => microsoftAccountClient.SignOutAsync(account.UniqueId);
}
