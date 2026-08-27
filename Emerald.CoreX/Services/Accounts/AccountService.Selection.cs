using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
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
                EnsureAccountUsableCore(matched);

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

    public AccountProviderUsability GetProviderUsability(string providerId)
    {
        AccountProviderUsability result = null!;
        _uiDispatcher.Invoke(() => result = GetProviderUsabilityCore(providerId));
        return result;
    }

    public AccountProviderUsability GetAccountUsability(EAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        AccountProviderUsability result = null!;
        _uiDispatcher.Invoke(() => result = GetAccountUsabilityCore(account));
        return result;
    }

    private AccountProviderUsability GetProviderUsabilityCore(string providerId)
    {
        if (!_providers.TryGetValue(providerId, out var provider))
            return new AccountProviderUsability(false, $"Unknown account provider: {providerId}");

        var descriptor = provider.Descriptor;
        if (!descriptor.IsConfigured)
        {
            return new AccountProviderUsability(
                false,
                descriptor.ConfigurationMessage ?? $"{descriptor.DisplayName} is not configured.");
        }

        foreach (var requirement in descriptor.EffectiveRequirements)
        {
            if (!_accounts.Any(account => string.Equals(account.ProviderId, requirement.ProviderId, StringComparison.Ordinal)))
                return new AccountProviderUsability(false, requirement.UnavailableMessage);
        }

        return AccountProviderUsability.Available;
    }

    private AccountProviderUsability GetAccountUsabilityCore(EAccount account)
    {
        EnsureProviderId(account);
        if (!_providers.TryGetValue(account.ProviderId, out var provider))
            return new AccountProviderUsability(false, $"Unknown account provider: {account.ProviderId}");

        foreach (var requirement in provider.Descriptor.EffectiveRequirements)
        {
            if (!_accounts.Any(candidate => string.Equals(candidate.ProviderId, requirement.ProviderId, StringComparison.Ordinal)))
                return new AccountProviderUsability(false, requirement.UnavailableMessage);
        }

        return provider.GetAccountUsability(account);
    }

    private void EnsureProviderUsableCore(string providerId)
    {
        var usability = GetProviderUsabilityCore(providerId);
        if (!usability.IsAvailable)
            throw new InvalidOperationException(usability.UnavailableReason);
    }

    private void EnsureAccountUsableCore(EAccount account)
    {
        var usability = GetAccountUsabilityCore(account);
        if (!usability.IsAvailable)
            throw new InvalidOperationException(usability.UnavailableReason);
    }

    private void EnforceAccountSelectionPoliciesCore(bool persist)
    {
        var selectedAccount = GetSelectedAccountCore();

        if (selectedAccount is not null && !GetAccountUsabilityCore(selectedAccount).IsAvailable)
        {
            _logger.LogInformation(
                "Clearing selection for account {AccountId} because provider {ProviderId} is unavailable.",
                selectedAccount.UniqueId,
                selectedAccount.ProviderId);
            ApplySelectedAccountCore(null, persist);
        }
    }
}
