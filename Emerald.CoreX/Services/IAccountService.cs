using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services;

public interface IAccountService
{
    ObservableCollection<EAccount> Accounts { get; }
    bool RequireMicrosoftAccountForOfflineAccounts { get; }
    bool RequireMicrosoftAccountForElyByAccounts { get; }
    Task LoadAllAccountsAsync();
    void CreateOfflineAccount(string username);
    Task SignInMicrosoftAccountAsync(CancellationToken cancellationToken = default);
    Task SignInElyByAccountAsync(CancellationToken cancellationToken = default);
    Task SignInElyByAccountAsync(
        string login,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancellationToken = default);
    Task RemoveAccountAsync(EAccount account);
    Task<GameAuthenticationResult> AuthenticateAccountAsync(EAccount account);
    EAccount? GetMostRecentlyUsedAccount();
    EAccount? GetSelectedAccount();
    void SetSelectedAccount(EAccount? account);
    Task InitializeAsync(string clientId);
}
