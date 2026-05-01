using CmlLib.Core.Auth;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;

namespace Emerald.CoreX.Services.Auth.Offline;

internal sealed class OfflineAccountAuthenticationProvider : IAccountAuthenticationProvider
{
    public AccountType AccountType => AccountType.Offline;
    public string ProviderId => AccountProviderIds.Offline;

    public Task<GameAuthenticationResult> AuthenticateAsync(EAccount account, CancellationToken cancellationToken = default)
        => Task.FromResult(new GameAuthenticationResult(MSession.CreateOfflineSession(account.Name)));

    public Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
