using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    private void PersistAccounts()
    {
        try
        {
            List<EAccount> storedAccounts = [];
            string? selectedAccountId = null;
            _uiDispatcher.Invoke(() =>
            {
                storedAccounts = _accounts
                    .Select(CloneStoredAccount)
                    .ToList();
                selectedAccountId = _selectedAccountId;
            });

            _settingsService.Set(SettingsKeys.MinecraftAccounts, storedAccounts);
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, selectedAccountId);
            _logger.LogDebug(
                "Persisted {AccountCount} accounts. SelectedAccountId: {SelectedAccountId}.",
                storedAccounts.Count,
                selectedAccountId ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist accounts.");
            throw;
        }
    }

    private EAccount EnsureUniqueId(EAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.UniqueId))
        {
            account.UniqueId = Guid.NewGuid().ToString();
            _logger.LogInformation(
                "Generated missing UniqueId for account '{Name}' ({Type}).",
                account.Name,
                account.Type);
        }

        return account;
    }

    private static void EnsureProviderId(EAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.ProviderId))
            account.ProviderId = AccountProviderIds.FromAccountType(account.Type);

        if (string.IsNullOrWhiteSpace(account.ProviderDisplayName))
            account.ProviderDisplayName = AccountProviderIds.GetDisplayName(account.ProviderId);
    }

    private static void ApplyProviderMetadata(EAccount account, AccountProviderDescriptor descriptor)
    {
        account.ProviderId = descriptor.ProviderId;
        account.ProviderDisplayName = descriptor.DisplayName;
        account.ProviderActions = descriptor.EffectiveActions;
    }

    private EAccount CloneStoredAccount(EAccount account)
    {
        var storedAccount = EnsureUniqueId(account);
        EnsureProviderId(storedAccount);
        return new EAccount(storedAccount.Name, storedAccount.Type, storedAccount.UUID, storedAccount.UniqueId)
        {
            LastUsed = storedAccount.LastUsed,
            ProviderId = storedAccount.ProviderId
        };
    }
}
