using Emerald.CoreX.Helpers;
using Emerald.Services;

namespace Emerald.CoreX.Store;

public interface IStoreInstallRecordRepository
{
    StoreInstallRecord[] GetAll();

    void Save(IEnumerable<StoreInstallRecord> records);

    IReadOnlyList<StoreInstallRecord> GetForGameAndType(string gamePath, StoreContentType contentType);

    IReadOnlyList<StoreInstallRecord> FindByFilePath(
        StoreContentType contentType,
        string filePath,
        string? gamePath = null);

    bool IsForGameAndType(StoreInstallRecord record, string gamePath, StoreContentType contentType);
}

public sealed class StoreInstallRecordRepository : IStoreInstallRecordRepository
{
    private readonly IBaseSettingsService _baseSettingsService;

    public StoreInstallRecordRepository(IBaseSettingsService baseSettingsService)
    {
        _baseSettingsService = baseSettingsService;
    }

    public StoreInstallRecord[] GetAll()
        => _baseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>());

    public void Save(IEnumerable<StoreInstallRecord> records)
        => _baseSettingsService.Set(SettingsKeys.StoreInstalledItems, records.ToArray());

    public IReadOnlyList<StoreInstallRecord> GetForGameAndType(string gamePath, StoreContentType contentType)
        => GetAll()
            .Where(record => IsForGameAndType(record, gamePath, contentType))
            .ToArray();

    public IReadOnlyList<StoreInstallRecord> FindByFilePath(
        StoreContentType contentType,
        string filePath,
        string? gamePath = null)
    {
        var normalizedFilePath = StorePath.Normalize(filePath);
        return GetAll()
            .Where(record =>
                record.ContentType == contentType
                && (string.IsNullOrWhiteSpace(gamePath) || StorePath.EqualsPath(record.GamePath, gamePath))
                && string.Equals(StorePath.Normalize(record.FilePath), normalizedFilePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public bool IsForGameAndType(StoreInstallRecord record, string gamePath, StoreContentType contentType)
        => record.ContentType == contentType
           && StorePath.EqualsPath(record.GamePath, gamePath);
}
