using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth.Microsoft;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    public async Task LoadAllAccountsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _loadGate.WaitAsync().ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Loading accounts from Emerald settings, the CmlLib account store, and Ely.by account store.");
            var loadState = BuildAccountLoadState();

            _logger.LogInformation(
                "Found {OfflineCount} offline accounts, {StoredMicrosoftCount} stored Microsoft accounts, {ElyByCount} Ely.by accounts, and {OnlineCount} Microsoft accounts in CmlLib.",
                loadState.OfflineCount,
                loadState.StoredMicrosoftCount,
                loadState.ElyByCount,
                loadState.MicrosoftCount);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _uiDispatcher.InvokeAsync(() =>
                {
                    ApplyLoadedAccountsCore(loadState.Accounts);
                }).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            PersistAccounts();
            NotifyLoggedOutMicrosoftAccounts(loadState.LoggedOutMicrosoftAccountNames);
            _logger.LogInformation(
                "Loaded {TotalCount} accounts ({OfflineCount} offline, {MicrosoftCount} Microsoft, {ElyByCount} Ely.by). Logged out Microsoft accounts detected: {LoggedOutMicrosoftCount}.",
                loadState.TotalCount,
                loadState.OfflineCount,
                loadState.MicrosoftCount,
                loadState.ElyByCount,
                loadState.LoggedOutMicrosoftCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load accounts.");
            throw;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private AccountLoadState BuildAccountLoadState()
    {
        var storedAccounts = _settingsService.Get(SettingsKeys.MinecraftAccounts, new List<EAccount>());
        var offlineAccounts = storedAccounts
            .Where(account => account.Type == AccountType.Offline)
            .Select(CloneStoredAccount)
            .ToList();
        var storedMicrosoftAccounts = storedAccounts
            .Where(account => account.Type == AccountType.Microsoft)
            .Select(CloneStoredAccount)
            .ToList();
        var storedElyByAccounts = _elyByAccountStore.GetAccounts()
            .Select(CreateElyByAccount)
            .ToList();
        var onlineMicrosoftAccounts = _microsoftAccountClient.GetAccounts()
            .Where(account =>
            {
                if (!string.IsNullOrWhiteSpace(account.Identifier))
                    return true;

                _logger.LogWarning("Skipping a Microsoft account with a missing identifier.");
                return false;
            })
            .Select(CreateMicrosoftAccount)
            .ToList();

        var onlineIdentifiers = new HashSet<string>(
            onlineMicrosoftAccounts.Select(account => account.UniqueId),
            StringComparer.Ordinal);
        var loggedOutAccountNames = storedMicrosoftAccounts
            .Where(account => !onlineIdentifiers.Contains(account.UniqueId))
            .Select(account => account.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var loadedAccounts = new List<EAccount>(offlineAccounts.Count + onlineMicrosoftAccounts.Count + storedElyByAccounts.Count);
        loadedAccounts.AddRange(offlineAccounts);
        loadedAccounts.AddRange(onlineMicrosoftAccounts);
        loadedAccounts.AddRange(storedElyByAccounts);

        return new AccountLoadState(
            loadedAccounts,
            offlineAccounts.Count,
            storedMicrosoftAccounts.Count,
            onlineMicrosoftAccounts.Count,
            storedElyByAccounts.Count,
            loggedOutAccountNames);
    }

    private void NotifyLoggedOutMicrosoftAccounts(IReadOnlyList<string> loggedOutMicrosoftAccountNames)
    {
        if (_notificationService is null || loggedOutMicrosoftAccountNames.Count == 0)
            return;

        if (loggedOutMicrosoftAccountNames.Count == 1)
        {
            _notificationService.Warning(
                "Microsoft account signed out",
                $"'{loggedOutMicrosoftAccountNames[0]}' is no longer signed in and was removed from Accounts.");
            return;
        }

        _notificationService.Warning(
            "Microsoft accounts signed out",
            $"{loggedOutMicrosoftAccountNames.Count} Microsoft accounts are no longer signed in and were removed from Accounts.");
    }

    private static EAccount CreateMicrosoftAccount(MicrosoftAccountInfo account)
        => new(
            account.Name,
            AccountType.Microsoft,
            string.IsNullOrWhiteSpace(account.UUID) ? account.Identifier : account.UUID,
            account.Identifier)
        {
            LastUsed = account.LastAccess == default ? DateTime.UtcNow : account.LastAccess,
            ProviderId = AccountProviderIds.Microsoft
        };

    private static EAccount CreateElyByAccount(ElyByStoredAccount account)
        => new(
            account.Name,
            AccountType.ElyBy,
            account.UUID,
            account.UniqueId)
        {
            LastUsed = account.LastUsed == default ? DateTime.UtcNow : account.LastUsed,
            ProviderId = AccountProviderIds.ElyBy
        };

    private static ElyByStoredAccount CreateStoredElyByAccount(ElyByAuthSession session)
        => new()
        {
            UniqueId = CreateElyByUniqueId(session.UUID),
            Name = session.Name,
            UUID = session.UUID,
            AccessToken = session.AccessToken,
            ClientToken = session.ClientToken,
            LastUsed = DateTime.UtcNow
        };

    private static string CreateElyByUniqueId(string uuid)
        => $"{AccountProviderIds.ElyBy}:{uuid}";

    private void ApplyLoadedAccountsCore(IEnumerable<EAccount> accounts)
    {
        _accounts.Clear();

        foreach (var account in accounts)
        {
            EnsureProviderId(account);
            _accounts.Add(account);
        }

        RestoreSelectedAccountCore();
        EnforceAccountSelectionPoliciesCore(persist: false);
    }
}
