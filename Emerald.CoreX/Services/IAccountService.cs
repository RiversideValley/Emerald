using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services;

public interface IAccountService
{
    ObservableCollection<EAccount> Accounts { get; }
    Task LoadAllAccountsAsync();
    void CreateOfflineAccount(string username);
    Task SignInMicrosoftAccountAsync();
    Task RemoveAccountAsync(EAccount account);
    Task<MSession> AuthenticateAccountAsync(EAccount account);
    EAccount? GetMostRecentlyUsedAccount();
    Task InitializeAsync(string clientId);
}
