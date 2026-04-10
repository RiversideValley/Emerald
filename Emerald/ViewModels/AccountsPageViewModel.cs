using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Microsoft.Extensions.Logging;
using System;

namespace Emerald.ViewModels;

public partial class AccountsPageViewModel : ObservableObject
{
    private readonly IAccountService _accountService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AccountsPageViewModel> _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    private string? _loadErrorMessage;

    [ObservableProperty]
    private string _offlineUsername = string.Empty;

    public ObservableCollection<EAccount> Accounts => _accountService.Accounts;
    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadErrorMessage);
    public EAccount? SelectedAccount => _accountService.GetSelectedAccount();
    public bool CanCreateOfflineAccount
        => !_accountService.RequireMicrosoftAccountForOfflineAccounts
           || Accounts.Any(account => account.Type == AccountType.Microsoft);
    public bool ShowOfflineAccountRestriction
        => _accountService.RequireMicrosoftAccountForOfflineAccounts && !CanCreateOfflineAccount;

    public AccountsPageViewModel(IAccountService accountService, INotificationService notificationService, ILogger<AccountsPageViewModel> logger)
    {
        _accountService = accountService;
        _notificationService = notificationService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (Accounts.Count > 0 && !HasLoadError) return;

        IsLoading = true;
        LoadErrorMessage = null;
        try
        {
            await _accountService.LoadAllAccountsAsync();
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load accounts.");
            LoadErrorMessage = "Could not load accounts.";
            _notificationService.Error("AccountLoadError", "Could not load accounts.", ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddMicrosoftAccountAsync()
    {
        IsLoading = true;
        LoadErrorMessage = null;
        try
        {
            await _accountService.SignInMicrosoftAccountAsync();
            _notificationService.Info("AccountAdded", "Microsoft account added successfully!");
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign in with Microsoft account.");
            LoadErrorMessage = "Failed to add Microsoft account.";
            _notificationService.Error("SignInError", "Failed to add Microsoft account.", ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddOfflineAccount()
    {
        if (string.IsNullOrWhiteSpace(OfflineUsername))
        {
            _notificationService.Warning("InvalidUsername", "Offline username cannot be empty.");
            return;
        }

        try
        {
            _accountService.CreateOfflineAccount(OfflineUsername);
            LoadErrorMessage = null;
            _notificationService.Info("AccountAdded", $"Offline account '{OfflineUsername}' created.");
            OfflineUsername = string.Empty; // Clear for next use
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create offline account.");
            _notificationService.Error("CreateOfflineError", "Could not create offline account.", ex: ex);
        }
    }

    [RelayCommand]
    private async Task RemoveAccountAsync(EAccount? account)
    {
        if (account is null) return;

        try
        {
            await _accountService.RemoveAccountAsync(account);
            _notificationService.Info("AccountRemoved", $"Account '{account.Name}' has been removed.");
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove account.");
            _notificationService.Error("RemoveAccountError", "Could not remove the account.", ex: ex);
        }
    }

    [RelayCommand]
    private async Task ActivateAccountAsync(EAccount? account)
    {
        if (account is null)
        {
            return;
        }

        if (account.IsSelected)
        {
            return;
        }

        IsLoading = true;
        LoadErrorMessage = null;

        try
        {
            await _accountService.AuthenticateAccountAsync(account);
            _accountService.SetSelectedAccount(account);
            _notificationService.Info("AccountSelected", $"'{account.Name}' is now selected for launches.");
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate account {AccountName}.", account.Name);
            LoadErrorMessage = $"Failed to authenticate '{account.Name}'.";
            _notificationService.Error("AccountSelectError", $"Could not switch to '{account.Name}'.", ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void NotifyAccountStateChanged()
    {
        OnPropertyChanged(nameof(SelectedAccount));
        OnPropertyChanged(nameof(CanCreateOfflineAccount));
        OnPropertyChanged(nameof(ShowOfflineAccountRestriction));
    }
}
