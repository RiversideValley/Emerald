using Emerald.CoreX.Helpers;
using Emerald.CoreX.Services;
using Emerald.Services;

namespace Emerald.CoreX.Store;

public interface IStoreInstallRecordRepository
{
    void LoadForBasePath(string basePath);

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
    private readonly IMinecraftBaseSettingsService _minecraftBaseSettingsService;

    public StoreInstallRecordRepository(
        IBaseSettingsService baseSettingsService,
        IMinecraftBaseSettingsService minecraftBaseSettingsService)
    {
        _baseSettingsService = baseSettingsService;
        _minecraftBaseSettingsService = minecraftBaseSettingsService;
    }

    public void LoadForBasePath(string basePath)
    {
        _minecraftBaseSettingsService.UseBasePath(basePath);
        if (_minecraftBaseSettingsService.Exists(SettingsKeys.StoreInstalledItems))
        {
            return;
        }

        if (!_baseSettingsService.Exists(SettingsKeys.StoreInstalledItems))
        {
            _minecraftBaseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>());
            return;
        }

        var centralRecords = _baseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>());
        var migratedRecords = centralRecords
            .Where(record => IsPathInBase(record.GamePath, basePath))
            .ToArray();

        if (migratedRecords.Length == 0)
        {
            _minecraftBaseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>());
            return;
        }

        _minecraftBaseSettingsService.Set(SettingsKeys.StoreInstalledItems, migratedRecords);

        var remainingRecords = centralRecords
            .Where(record => !IsPathInBase(record.GamePath, basePath))
            .ToArray();
        if (remainingRecords.Length == 0)
        {
            _baseSettingsService.Delete(SettingsKeys.StoreInstalledItems);
        }
        else
        {
            _baseSettingsService.Set(SettingsKeys.StoreInstalledItems, remainingRecords);
        }
    }

    public StoreInstallRecord[] GetAll()
        => _minecraftBaseSettingsService.IsInitialized
            ? _minecraftBaseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>())
            : _baseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>());

    public void Save(IEnumerable<StoreInstallRecord> records)
    {
        if (_minecraftBaseSettingsService.IsInitialized)
        {
            _minecraftBaseSettingsService.Set(SettingsKeys.StoreInstalledItems, records.ToArray());
        }
        else
        {
            _baseSettingsService.Set(SettingsKeys.StoreInstalledItems, records.ToArray());
        }
    }

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

    private static bool IsPathInBase(string path, string basePath)
        => StorePath.EqualsPath(path, basePath)
           || StorePath.IsInsideRoot(path, StorePath.Normalize(basePath));
}
