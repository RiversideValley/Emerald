using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    private CancellationTokenSource? _loginCancellationSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingMessage))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingMessage))]
    [NotifyPropertyChangedFor(nameof(CanStartMicrosoftLogin))]
    [NotifyPropertyChangedFor(nameof(CanStartElyByLogin))]
    [NotifyPropertyChangedFor(nameof(CanStartOfflineAccount))]
    [NotifyPropertyChangedFor(nameof(CanCancelLogin))]
    [NotifyCanExecuteChangedFor(nameof(AddMicrosoftAccountCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddElyByAccountCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddOfflineAccountCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoginCommand))]
    private bool _isLoginInProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingMessage))]
    private string _loginStatusMessage = "Loading accounts...";

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
    public bool CanCreateElyByAccount
        => !_accountService.RequireMicrosoftAccountForElyByAccounts
           || Accounts.Any(account => account.Type == AccountType.Microsoft);
    public bool CanStartMicrosoftLogin => !IsLoginInProgress;
    public bool CanStartElyByLogin => CanCreateElyByAccount && !IsLoginInProgress;
    public bool CanStartOfflineAccount => CanCreateOfflineAccount && !IsLoginInProgress;
    public bool CanCancelLogin
        => IsLoginInProgress && _loginCancellationSource is { IsCancellationRequested: false };
    public string LoadingMessage => IsLoginInProgress ? LoginStatusMessage : "Loading accounts...";
    public bool ShowOfflineAccountRestriction
        => _accountService.RequireMicrosoftAccountForOfflineAccounts && !CanCreateOfflineAccount;
    public bool ShowElyByAccountRestriction
        => _accountService.RequireMicrosoftAccountForElyByAccounts && !CanCreateElyByAccount;

    public AccountsPageViewModel(IAccountService accountService, INotificationService notificationService, ILogger<AccountsPageViewModel> logger)
    {
        _accountService = accountService;
        _notificationService = notificationService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (IsLoginInProgress)
        {
            NotifyAccountStateChanged();
            return;
        }

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

    [RelayCommand(CanExecute = nameof(CanStartMicrosoftLogin))]
    private async Task AddMicrosoftAccountAsync()
    {
        var cancellationToken = BeginLogin("Complete Microsoft sign-in in your browser.");
        try
        {
            await _accountService.SignInMicrosoftAccountAsync(cancellationToken);
            _notificationService.Info("AccountAdded", "Microsoft account added successfully!");
            NotifyAccountStateChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Microsoft sign-in was canceled.");
            LoadErrorMessage = null;
            _notificationService.Info("SignInCanceled", "Microsoft sign-in canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign in with Microsoft account.");
            LoadErrorMessage = "Failed to add Microsoft account.";
            _notificationService.Error("SignInError", "Failed to add Microsoft account.", ex: ex);
        }
        finally
        {
            EndLogin(cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartElyByLogin))]
    private async Task AddElyByAccountAsync()
    {
        if (!CanCreateElyByAccount)
        {
            _notificationService.Warning("ElyByNeedsMicrosoft", "Sign in with a Microsoft account before adding Ely.by accounts.");
            return;
        }

        var cancellationToken = BeginLogin("Complete Ely.by sign-in in your browser.");
        try
        {
            _notificationService.Info("UsingBrowser", "Complete Ely.by sign-in in your browser.");
            await _accountService.SignInElyByAccountAsync(cancellationToken);
            _notificationService.Info("AccountAdded", "Ely.by account added successfully!");
            NotifyAccountStateChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Ely.by sign-in was canceled.");
            LoadErrorMessage = null;
            _notificationService.Info("SignInCanceled", "Ely.by sign-in canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign in with Ely.by account.");
            LoadErrorMessage = "Failed to add Ely.by account.";
            _notificationService.Error("ElyBySignInError", "Failed to add Ely.by account.", ex: ex);
        }
        finally
        {
            EndLogin(cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartOfflineAccount))]
    private void AddOfflineAccount()
    {
        if (!CanStartOfflineAccount)
        {
            return;
        }

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

    [RelayCommand(CanExecute = nameof(CanCancelLogin))]
    private void CancelLogin()
    {
        if (_loginCancellationSource is not { IsCancellationRequested: false })
        {
            return;
        }

        LoginStatusMessage = "Canceling sign-in...";
        _loginCancellationSource.Cancel();
        NotifyLoginCommandStateChanged();
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

    private CancellationToken BeginLogin(string statusMessage)
    {
        _loginCancellationSource?.Dispose();
        _loginCancellationSource = new CancellationTokenSource();
        LoginStatusMessage = statusMessage;
        LoadErrorMessage = null;
        IsLoginInProgress = true;
        IsLoading = true;
        NotifyAccountStateChanged();
        NotifyLoginCommandStateChanged();
        return _loginCancellationSource.Token;
    }

    private void EndLogin(CancellationToken cancellationToken)
    {
        if (_loginCancellationSource is null || !_loginCancellationSource.Token.Equals(cancellationToken))
        {
            return;
        }

        _loginCancellationSource.Dispose();
        _loginCancellationSource = null;
        IsLoginInProgress = false;
        LoginStatusMessage = "Loading accounts...";
        IsLoading = false;
        NotifyAccountStateChanged();
        NotifyLoginCommandStateChanged();
    }

    private void NotifyAccountStateChanged()
    {
        OnPropertyChanged(nameof(SelectedAccount));
        OnPropertyChanged(nameof(CanCreateOfflineAccount));
        OnPropertyChanged(nameof(CanCreateElyByAccount));
        OnPropertyChanged(nameof(CanStartMicrosoftLogin));
        OnPropertyChanged(nameof(CanStartElyByLogin));
        OnPropertyChanged(nameof(CanStartOfflineAccount));
        OnPropertyChanged(nameof(CanCancelLogin));
        OnPropertyChanged(nameof(ShowOfflineAccountRestriction));
        OnPropertyChanged(nameof(ShowElyByAccountRestriction));
        NotifyLoginCommandStateChanged();
    }

    private void NotifyLoginCommandStateChanged()
    {
        AddMicrosoftAccountCommand.NotifyCanExecuteChanged();
        AddElyByAccountCommand.NotifyCanExecuteChanged();
        AddOfflineAccountCommand.NotifyCanExecuteChanged();
        CancelLoginCommand.NotifyCanExecuteChanged();
    }
}
