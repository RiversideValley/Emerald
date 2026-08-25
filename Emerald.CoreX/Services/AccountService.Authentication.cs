using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    public async Task RemoveAccountAsync(EAccount account)
    {
        _logger.LogInformation("Removing account '{Name}' ({Type}).", account.Name, account.Type);

        if (account.Type == AccountType.Microsoft)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            await GetAuthenticationProvider(account.Type).RemoveAsync(account).ConfigureAwait(false);
            _logger.LogInformation("Signed out Microsoft account '{Name}' ({Identifier}).", account.Name, account.UniqueId);
            await LoadAllAccountsAsync().ConfigureAwait(false);
            return;
        }

        if (account.Type == AccountType.ElyBy)
        {
            await GetAuthenticationProvider(account.Type).RemoveAsync(account).ConfigureAwait(false);
            _logger.LogInformation("Signed out Ely.by account '{Name}' ({Identifier}).", account.Name, account.UniqueId);
        }

        var wasSelected = false;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                wasSelected = string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal);
                _accounts.Remove(account);

                if (wasSelected)
                    ApplySelectedAccountCore(null, persist: false);

                EnforceAccountSelectionPoliciesCore(persist: false);
            }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        PersistAccounts();
    }

    public async Task<GameAuthenticationResult> AuthenticateAccountAsync(EAccount account)
    {
        _logger.LogInformation("Authenticating '{Name}' ({Type}).", account.Name, account.Type);

        if (account.Type == AccountType.Offline)
        {
            EnsureOfflineAccountPolicyMet("Offline accounts require at least one Microsoft account.");
        }
        else if (account.Type == AccountType.ElyBy)
        {
            EnsureElyByAccountPolicyMet("Ely.by accounts require at least one Microsoft account.");
        }
        else if (account.Type == AccountType.Microsoft)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
        }

        var authenticationResult = await GetAuthenticationProvider(account.Type)
            .AuthenticateAsync(account)
            .ConfigureAwait(false);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                var matched = _accounts.FirstOrDefault(candidate =>
                    ReferenceEquals(candidate, account) ||
                    string.Equals(candidate.UniqueId, account.UniqueId, StringComparison.Ordinal));

                if (matched is not null)
                    matched.LastUsed = DateTime.UtcNow;
            }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        PersistAccounts();
        return authenticationResult;
    }

    public async Task<GameAuthenticationResult> AuthenticateLaunchAccountAsync(EAccount account, bool useOfflineFallback)
    {
        if (!useOfflineFallback || account.Type == AccountType.Offline)
        {
            return await AuthenticateAccountAsync(account).ConfigureAwait(false);
        }

        var (offlineAccount, created) = EnsureOfflineLaunchAccount(account);
        _logger.LogInformation(
            "Using offline launch account '{OfflineName}' for selected {AccountType} account '{SelectedName}'. Created: {Created}.",
            offlineAccount.Name,
            account.Type,
            account.Name,
            created);

        if (created)
        {
            _notificationService?.Info(
                "OfflineMode",
                $"Created offline account '{offlineAccount.Name}' for this launch.");
        }

        return await AuthenticateAccountAsync(offlineAccount).ConfigureAwait(false);
    }

    private (EAccount Account, bool Created) EnsureOfflineLaunchAccount(EAccount sourceAccount)
    {
        var username = string.IsNullOrWhiteSpace(sourceAccount.Name)
            ? "Player"
            : sourceAccount.Name.Trim();
        EAccount? offlineAccount = null;
        var created = false;

        _gate.Wait();
        try
        {
            _uiDispatcher.Invoke(() =>
            {
                offlineAccount = _accounts.FirstOrDefault(candidate =>
                    candidate.Type == AccountType.Offline &&
                    candidate.Name.Equals(username, StringComparison.OrdinalIgnoreCase));

                if (offlineAccount is not null)
                {
                    return;
                }

                offlineAccount = new EAccount(username, AccountType.Offline);
                _accounts.Add(offlineAccount);
                created = true;
            });
        }
        finally
        {
            _gate.Release();
        }

        if (created)
        {
            PersistAccounts();
        }

        return (offlineAccount!, created);
    }

    private IAccountAuthenticationProvider GetAuthenticationProvider(AccountType accountType)
        => _authenticationProviders.TryGetValue(accountType, out var provider)
            ? provider
            : throw new ArgumentException($"Unknown account type: {accountType}");
}
