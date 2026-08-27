using Emerald.CoreX.Helpers;
using Emerald.Services;

namespace Emerald.CoreX.Services.Auth.ElyBy;

internal sealed class ElyByAccountStore(IBaseSettingsService settingsService) : IElyByAccountStore
{
    public IReadOnlyList<ElyByStoredAccount> GetAccounts()
        => settingsService.Get(SettingsKeys.ElyByAccounts, new List<ElyByStoredAccount>())
            .Where(account => !string.IsNullOrWhiteSpace(account.UniqueId))
            .ToList();

    public ElyByStoredAccount? Find(string uniqueId)
        => GetAccounts().FirstOrDefault(account => string.Equals(account.UniqueId, uniqueId, StringComparison.Ordinal));

    public void Upsert(ElyByStoredAccount account)
    {
        var accounts = GetAccounts().ToList();
        var index = accounts.FindIndex(candidate => string.Equals(candidate.UniqueId, account.UniqueId, StringComparison.Ordinal));

        if (index >= 0)
            accounts[index] = account;
        else
            accounts.Add(account);

        settingsService.Set(SettingsKeys.ElyByAccounts, accounts);
    }

    public void Remove(string uniqueId)
    {
        var accounts = GetAccounts()
            .Where(account => !string.Equals(account.UniqueId, uniqueId, StringComparison.Ordinal))
            .ToList();

        settingsService.Set(SettingsKeys.ElyByAccounts, accounts);
    }
}
