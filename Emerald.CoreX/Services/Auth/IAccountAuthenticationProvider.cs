using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth;

public interface IAccountAuthenticationProvider
{
    AccountType AccountType { get; }
    string ProviderId { get; }
    Task<GameAuthenticationResult> AuthenticateAsync(EAccount account, CancellationToken cancellationToken = default);
    Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default);
}
