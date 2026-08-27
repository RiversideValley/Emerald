using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth;
using Emerald.Helpers;
using Microsoft.Extensions.Logging;

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
    [NotifyPropertyChangedFor(nameof(CanCancelLogin))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoginCommand))]
    private bool _isLoginInProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingMessage))]
    private string _loginStatusMessage = "AccountsLoading".Localize();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    private string? _loadErrorMessage;

    public ObservableCollection<EAccount> Accounts => _accountService.Accounts;
    public IReadOnlyList<AccountProviderDescriptor> Providers => _accountService.Providers;
    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadErrorMessage);
    public EAccount? SelectedAccount => _accountService.GetSelectedAccount();
    public AccountProviderUsability GetProviderUsability(string providerId)
        => _accountService.GetProviderUsability(providerId);
    public bool CanCancelLogin
        => IsLoginInProgress && _loginCancellationSource is { IsCancellationRequested: false };
    public string LoadingMessage => IsLoginInProgress ? LoginStatusMessage : "AccountsLoading".Localize();

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
            LoadErrorMessage = "AccountLoadFailedMessage".Localize();
            _notificationService.Error("AccountLoadFailedTitle".Localize(), LoadErrorMessage, ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddProviderAccountAsync(
        string providerId,
        string methodId,
        string? username = null)
    {
        var provider = Providers.FirstOrDefault(candidate => candidate.ProviderId == providerId);
        if (provider is null)
        {
            _notificationService.Error(
                "AccountProviderMissingTitle".Localize(),
                "AccountProviderMissingMessage".Localize());
            return;
        }

        var usability = _accountService.GetProviderUsability(providerId);
        if (!usability.IsAvailable)
        {
            var message = usability.UnavailableReason ?? $"{provider.DisplayName} is unavailable.";
            LoadErrorMessage = message;
            _notificationService.Warning("AccountProviderUnavailableTitle".Localize(), message);
            return;
        }

        var method = provider.SignInMethods.FirstOrDefault(candidate => candidate.MethodId == methodId);
        if (method is null)
        {
            _notificationService.Error(
                "AccountSignInMethodMissingTitle".Localize(),
                "AccountSignInMethodMissingMessage".Localize());
            return;
        }

        if (method.InputKind == AccountSignInInputKind.Username && string.IsNullOrWhiteSpace(username))
        {
            _notificationService.Warning("InvalidUsernameTitle".Localize(), "InvalidUsernameMessage".Localize());
            return;
        }

        var cancellationToken = BeginLogin(method.InputKind == AccountSignInInputKind.Username
            ? string.Format("AccountAddingFormat".Localize(), provider.DisplayName)
            : string.Format("AccountBrowserSignInFormat".Localize(), provider.DisplayName));
        try
        {
            await _accountService.SignInAsync(providerId, new AccountSignInRequest(methodId, username), cancellationToken);
            LoadErrorMessage = null;
            _notificationService.Info(
                "AccountAddedTitle".Localize(),
                string.Format("AccountAddedFormat".Localize(), provider.DisplayName));
            NotifyAccountStateChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LoadErrorMessage = null;
            _notificationService.Info(
                "SignInCanceledTitle".Localize(),
                string.Format("SignInCanceledFormat".Localize(), provider.DisplayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign in with {AccountProvider}.", providerId);
            LoadErrorMessage = ex.Message;
            _notificationService.Error(
                "AccountSignInFailedTitle".Localize(),
                string.Format("AccountSignInFailedFormat".Localize(), provider.DisplayName),
                ex: ex);
        }
        finally
        {
            EndLogin(cancellationToken);
        }
    }

    public async Task RefreshAccountAsync(EAccount account)
    {
        try
        {
            await _accountService.RefreshAccountAsync(account);
            LoadErrorMessage = null;
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh account {AccountName}.", account.Name);
            LoadErrorMessage = ex.Message;
            _notificationService.Error(
                "AccountRefreshFailedTitle".Localize(),
                string.Format("AccountRefreshFailedFormat".Localize(), account.Name),
                ex: ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelLogin))]
    private void CancelLogin()
    {
        if (_loginCancellationSource is not { IsCancellationRequested: false })
        {
            return;
        }

        LoginStatusMessage = "AccountCancelingSignIn".Localize();
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
            _notificationService.Info(
                "AccountRemovedTitle".Localize(),
                string.Format("AccountRemovedFormat".Localize(), account.Name));
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove account.");
            _notificationService.Error(
                "AccountRemoveFailedTitle".Localize(),
                "AccountRemoveFailedMessage".Localize(),
                ex: ex);
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
            await _accountService.RefreshAccountAsync(account);
            _accountService.SetSelectedAccount(account);
            _notificationService.Info(
                "AccountSelectedTitle".Localize(),
                string.Format("AccountSelectedFormat".Localize(), account.Name));
            NotifyAccountStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate account {AccountName}.", account.Name);
            LoadErrorMessage = string.Format("AccountAuthenticationFailedFormat".Localize(), account.Name);
            _notificationService.Error(
                "AccountSelectFailedTitle".Localize(),
                string.Format("AccountSelectFailedFormat".Localize(), account.Name),
                ex: ex);
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
        LoginStatusMessage = "AccountsLoading".Localize();
        IsLoading = false;
        NotifyAccountStateChanged();
        NotifyLoginCommandStateChanged();
    }

    private void NotifyAccountStateChanged()
    {
        OnPropertyChanged(nameof(SelectedAccount));
        OnPropertyChanged(nameof(Providers));
        OnPropertyChanged(nameof(CanCancelLogin));
        NotifyLoginCommandStateChanged();
    }

    private void NotifyLoginCommandStateChanged()
    {
        CancelLoginCommand.NotifyCanExecuteChanged();
    }
}
