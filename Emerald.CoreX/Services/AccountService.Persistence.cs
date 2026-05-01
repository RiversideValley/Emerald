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
            var offlineCount = storedAccounts.Count(account => account.Type == AccountType.Offline);
            var microsoftCount = storedAccounts.Count(account => account.Type == AccountType.Microsoft);
            var elyByCount = storedAccounts.Count(account => account.Type == AccountType.ElyBy);
            _logger.LogDebug(
                "Persisted {TotalCount} accounts ({OfflineCount} offline, {MicrosoftCount} Microsoft, {ElyByCount} Ely.by). SelectedAccountId: {SelectedAccountId}.",
                storedAccounts.Count,
                offlineCount,
                microsoftCount,
                elyByCount,
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
