using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
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
            _logger.LogInformation("Loading accounts from {ProviderCount} registered providers.", _providers.Count);
            var loadState = await LoadProvidersAsync().ConfigureAwait(false);

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
            PublishProviderNotices(loadState.Notices);
            _logger.LogInformation("Loaded {AccountCount} accounts from registered providers.", loadState.Accounts.Count);
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

    private async Task<AccountLoadState> LoadProvidersAsync()
    {
        var storedAccounts = _settingsService.Get(SettingsKeys.MinecraftAccounts, new List<EAccount>());
        foreach (var storedAccount in storedAccounts)
            EnsureProviderId(storedAccount);

        var loadedAccounts = new List<EAccount>();
        var notices = new List<AccountProviderNotice>();
        foreach (var provider in _providers.Values)
        {
            var result = await provider.LoadAccountsAsync(storedAccounts).ConfigureAwait(false);
            foreach (var account in result.Accounts)
            {
                ApplyProviderMetadata(account, provider.Descriptor);
                loadedAccounts.Add(account);
            }

            notices.AddRange(result.Notices);
            _logger.LogDebug(
                "Provider {ProviderId} loaded {AccountCount} accounts and emitted {NoticeCount} notices.",
                provider.Descriptor.ProviderId,
                result.Accounts.Count,
                result.Notices.Count);
        }

        return new AccountLoadState(loadedAccounts, notices);
    }

    private void PublishProviderNotices(IEnumerable<AccountProviderNotice> notices)
    {
        if (_notificationService is null)
            return;

        foreach (var notice in notices)
            _notificationService.Warning(notice.Title, notice.Message);
    }

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

    private sealed record AccountLoadState(
        IReadOnlyList<EAccount> Accounts,
        IReadOnlyList<AccountProviderNotice> Notices);
}
