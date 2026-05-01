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

    private IAccountAuthenticationProvider GetAuthenticationProvider(AccountType accountType)
        => _authenticationProviders.TryGetValue(accountType, out var provider)
            ? provider
            : throw new ArgumentException($"Unknown account type: {accountType}");
}
