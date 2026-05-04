using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    public EAccount? GetMostRecentlyUsedAccount()
    {
        EAccount? account = null;
        _uiDispatcher.Invoke(() =>
        {
            account = _accounts.Count == 0
                ? null
                : _accounts.OrderByDescending(candidate => candidate.LastUsed).First();
        });

        return account;
    }

    public EAccount? GetSelectedAccount()
    {
        EAccount? account = null;
        _uiDispatcher.Invoke(() => account = GetSelectedAccountCore());
        return account;
    }

    public void SetSelectedAccount(EAccount? account)
    {
        _gate.Wait();
        try
        {
            _uiDispatcher.Invoke(() =>
            {
                if (account is null)
                {
                    ApplySelectedAccountCore(null, persist: true);
                    return;
                }

                var matched = _accounts.FirstOrDefault(candidate =>
                    ReferenceEquals(candidate, account) ||
                    string.Equals(candidate.UniqueId, account.UniqueId, StringComparison.Ordinal));

                if (matched is null)
                {
                    _logger.LogWarning(
                        "SetSelectedAccount: account '{Name}' (id={Id}) not found in the collection.",
                        account.Name,
                        account.UniqueId);
                    return;
                }

                matched = EnsureUniqueId(matched);
                EnsureProviderId(matched);

                if (matched.Type == AccountType.Offline)
                    EnsureOfflineAccountPolicyMet("Selecting an offline account requires at least one Microsoft account.");

                if (matched.Type == AccountType.ElyBy)
                    EnsureElyByAccountPolicyMet("Selecting an Ely.by account requires at least one Microsoft account.");

                ApplySelectedAccountCore(matched.UniqueId, persist: true);
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RestoreSelectedAccountCore()
    {
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);

        if (!string.IsNullOrWhiteSpace(_selectedAccountId) && GetSelectedAccountCore() is null)
        {
            _logger.LogInformation("Previously selected account no longer exists; clearing selection.");
            ApplySelectedAccountCore(null, persist: false);
            return;
        }

        ApplySelectedAccountCore(_selectedAccountId, persist: false);
    }

    private EAccount? GetSelectedAccountCore()
        => string.IsNullOrWhiteSpace(_selectedAccountId)
            ? null
            : _accounts.FirstOrDefault(account =>
                string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal));

    private void ApplySelectedAccountCore(string? uniqueId, bool persist)
    {
        _selectedAccountId = string.IsNullOrWhiteSpace(uniqueId) ? null : uniqueId;

        foreach (var account in _accounts)
            account.IsSelected = string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal);

        if (persist)
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, _selectedAccountId);
    }

    private bool HasMicrosoftAccountCore()
        => _accounts.Any(account => account.Type == AccountType.Microsoft);

    private bool IsOfflineAccountAllowed()
        => !RequireMicrosoftAccountForOfflineAccounts || HasMicrosoftAccountCore();

    private bool IsElyByAccountAllowed()
        => !RequireMicrosoftAccountForElyByAccounts || HasMicrosoftAccountCore();

    private void EnsureOfflineAccountPolicyMet(string message)
    {
        if (!IsOfflineAccountAllowed())
            throw new InvalidOperationException(message);
    }

    private void EnsureElyByAccountPolicyMet(string message)
    {
        if (!IsElyByAccountAllowed())
            throw new InvalidOperationException(message);
    }

    private void EnforceAccountSelectionPoliciesCore(bool persist)
    {
        var selectedAccount = GetSelectedAccountCore();

        if (selectedAccount?.Type == AccountType.Offline && !IsOfflineAccountAllowed())
        {
            _logger.LogInformation("Clearing offline account selection due to policy.");
            ApplySelectedAccountCore(null, persist);
            return;
        }

        if (selectedAccount?.Type == AccountType.ElyBy && !IsElyByAccountAllowed())
        {
            _logger.LogInformation("Clearing Ely.by account selection due to policy.");
            ApplySelectedAccountCore(null, persist);
        }
    }
}
