using System.Collections.ObjectModel;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;

namespace Emerald.CoreX.Services;

/// <summary>
/// Coordinates registered account providers, shared metadata, and the selected
/// launch account. Provider-specific authentication stays in IAccountProvider.
/// </summary>
public interface IAccountService
{
    ObservableCollection<EAccount> Accounts { get; }
    IReadOnlyList<AccountProviderDescriptor> Providers { get; }

    AccountProviderUsability GetProviderUsability(string providerId);
    AccountProviderUsability GetAccountUsability(EAccount account);

    Task LoadAllAccountsAsync();
    Task<EAccount> SignInAsync(string providerId, AccountSignInRequest request, CancellationToken cancellationToken = default);
    Task RefreshAccountAsync(EAccount account, CancellationToken cancellationToken = default);
    Task<AccountSkinData> GetSkinAsync(EAccount account, bool forceRefresh = false, CancellationToken cancellationToken = default);
    Task RemoveAccountAsync(EAccount account);
    Task<GameAuthenticationResult> AuthenticateAccountAsync(EAccount account);
    Task<GameAuthenticationResult> AuthenticateLaunchAccountAsync(EAccount account, bool useOfflineFallback);
    EAccount? GetMostRecentlyUsedAccount();
    EAccount? GetSelectedAccount();
    void SetSelectedAccount(EAccount? account);
    Task InitializeAsync();
}
