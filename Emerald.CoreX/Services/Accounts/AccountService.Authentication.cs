using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    public async Task RemoveAccountAsync(EAccount account)
    {
        _logger.LogInformation("Removing account '{Name}' ({Type}).", account.Name, account.Type);

        await EnsureInitializedAsync().ConfigureAwait(false);
        await GetProvider(account).RemoveAsync(account).ConfigureAwait(false);

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

        await EnsureInitializedAsync().ConfigureAwait(false);
        _uiDispatcher.Invoke(() => EnsureAccountUsableCore(account));
        var authenticationResult = await GetProvider(account)
            .AuthenticateForLaunchAsync(account)
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
        EnsureProviderId(account);
        if (!useOfflineFallback || account.ProviderId == AccountProviderIds.Offline)
        {
            return await AuthenticateAccountAsync(account).ConfigureAwait(false);
        }

        var (offlineAccount, created) = await GetOrCreateOfflineLaunchAccountAsync(account).ConfigureAwait(false);
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

    private async Task<(EAccount Account, bool Created)> GetOrCreateOfflineLaunchAccountAsync(EAccount sourceAccount)
    {
        var username = string.IsNullOrWhiteSpace(sourceAccount.Name)
            ? "Player"
            : sourceAccount.Name.Trim();
        EAccount? offlineAccount = null;

        _gate.Wait();
        try
        {
            _uiDispatcher.Invoke(() =>
            {
                offlineAccount = _accounts.FirstOrDefault(candidate =>
                    candidate.ProviderId == AccountProviderIds.Offline &&
                    candidate.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
            });
        }
        finally
        {
            _gate.Release();
        }

        if (offlineAccount is not null)
            return (offlineAccount, false);

        var method = _providers[AccountProviderIds.Offline].Descriptor.SignInMethods
            .FirstOrDefault(candidate => candidate.InputKind == AccountSignInInputKind.Username)
            ?? throw new InvalidOperationException("The offline provider does not expose a username sign-in method.");
        offlineAccount = await SignInAsync(
            AccountProviderIds.Offline,
            new AccountSignInRequest(method.MethodId, username)).ConfigureAwait(false);
        return (offlineAccount, true);
    }

    public async Task RefreshAccountAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await EnsureInitializedAsync().ConfigureAwait(false);
        EnsureProviderId(account);
        _uiDispatcher.Invoke(() => EnsureAccountUsableCore(account));
        await GetProvider(account).RefreshAsync(account, cancellationToken).ConfigureAwait(false);
        PersistAccounts();
    }

    private IAccountProvider GetProvider(EAccount account)
    {
        EnsureProviderId(account);
        return _providers.TryGetValue(account.ProviderId, out var provider)
            ? provider
            : throw new ArgumentException($"Unknown account provider: {account.ProviderId}");
    }
}
