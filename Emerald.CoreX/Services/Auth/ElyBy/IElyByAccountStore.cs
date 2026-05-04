namespace Emerald.CoreX.Services.Auth.ElyBy;

internal interface IElyByAccountStore
{
    IReadOnlyList<ElyByStoredAccount> GetAccounts();
    ElyByStoredAccount? Find(string uniqueId);
    void Upsert(ElyByStoredAccount account);
    void Remove(string uniqueId);
}
