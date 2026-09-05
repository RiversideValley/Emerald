using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emerald.CoreX.CrashHandling;

public interface ICrashReportStore
{
    string ReportsPath { get; }

    bool TryWrite(CrashRecord record);
    bool TryWriteFatal(CrashRecord record);
    IReadOnlyList<CrashRecord> GetAll();
    CrashRecord? Get(string id);
    bool HasReportForRun(string runId);
    bool TryAcknowledge(string id);
    bool TryDelete(string id);
    int DeleteAll();
}

public sealed class FileCrashReportStore : ICrashReportStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();

    public FileCrashReportStore(string localDataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localDataPath);
        ReportsPath = Path.Combine(localDataPath, "crashes", "reports");
    }

    public string ReportsPath { get; }

    public bool TryWrite(CrashRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_gate)
        {
            try
            {
                return TryWriteCore(record);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Fatal paths must not wait behind a history operation that may be holding the
    /// normal store lock. Each report has a unique directory, so this write is safe
    /// to perform without that lock.
    /// </summary>
    public bool TryWriteFatal(CrashRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            return TryWriteCore(record);
        }
        catch
        {
            return false;
        }
    }

    private bool TryWriteCore(CrashRecord record)
    {
        Directory.CreateDirectory(ReportsPath);
        record.Id = NormalizeId(record.Id);

        var reportDirectory = Path.Combine(ReportsPath, record.Id);
        Directory.CreateDirectory(reportDirectory);

        var jsonPath = Path.Combine(reportDirectory, "report.json");
        var textPath = Path.Combine(reportDirectory, "report.txt");
        record.ReportPath = textPath;

        AtomicFile.WriteText(jsonPath, JsonSerializer.Serialize(record, JsonOptions));
        try
        {
            AtomicFile.WriteText(textPath, CrashReportFormatter.ToText(record));
        }
        catch
        {
            // The JSON record is still useful when the human-readable export fails.
        }

        return true;
    }

    public IReadOnlyList<CrashRecord> GetAll()
    {
        lock (_gate)
        {
            if (!Directory.Exists(ReportsPath))
            {
                return [];
            }

            var records = new List<CrashRecord>();
            IEnumerable<string> reportPaths;
            try
            {
                reportPaths = Directory
                    .EnumerateFiles(ReportsPath, "report.json", SearchOption.AllDirectories)
                    .ToArray();
            }
            catch
            {
                return [];
            }

            foreach (var path in reportPaths)
            {
                try
                {
                    var record = JsonSerializer.Deserialize<CrashRecord>(File.ReadAllText(path), JsonOptions);
                    if (record is null)
                    {
                        continue;
                    }

                    record.ReportPath ??= Path.Combine(Path.GetDirectoryName(path) ?? ReportsPath, "report.txt");
                    records.Add(record);
                }
                catch
                {
                    // A partially-written or manually damaged report must not break the history page.
                }
            }

            return records
                .OrderByDescending(record => record.OccurredUtc)
                .ThenByDescending(record => record.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public CrashRecord? Get(string id)
        => GetAll().FirstOrDefault(record => string.Equals(record.Id, id, StringComparison.Ordinal));

    public bool HasReportForRun(string runId)
        => !string.IsNullOrWhiteSpace(runId)
           && GetAll().Any(record => string.Equals(record.RunId, runId, StringComparison.Ordinal));

    public bool TryAcknowledge(string id)
    {
        lock (_gate)
        {
            var record = Get(id);
            if (record is null)
            {
                return false;
            }

            record.AcknowledgedUtc ??= DateTimeOffset.UtcNow;
            return TryWrite(record);
        }
    }

    public bool TryDelete(string id)
    {
        lock (_gate)
        {
            try
            {
                var directory = Path.Combine(ReportsPath, NormalizeId(id));
                if (!Directory.Exists(directory))
                {
                    return false;
                }

                Directory.Delete(directory, recursive: true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public int DeleteAll()
    {
        lock (_gate)
        {
            var count = 0;
            foreach (var record in GetAll())
            {
                if (TryDelete(record.Id))
                {
                    count++;
                }
            }

            return count;
        }
    }

    private static string NormalizeId(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id)
            && string.Equals(Path.GetFileName(id), id, StringComparison.Ordinal)
            && id.All(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
        {
            return id;
        }

        return Guid.NewGuid().ToString("N");
    }
}

/// <summary>
/// Keeps crash capture available when the preferred per-user location becomes
/// unwritable after startup (for example, a removed profile directory or a
/// deployment-specific storage restriction). Normal history operations read both
/// locations and preserve the report's actual path.
/// </summary>
public sealed class FallbackCrashReportStore : ICrashReportStore
{
    private readonly IReadOnlyList<ICrashReportStore> _stores;

    public FallbackCrashReportStore(params ICrashReportStore[] stores)
    {
        if (stores is null || stores.Length == 0)
        {
            throw new ArgumentException("At least one crash report store is required.", nameof(stores));
        }

        _stores = stores
            .Where(store => store is not null)
            .GroupBy(store => store.ReportsPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (_stores.Count == 0)
        {
            throw new ArgumentException("At least one crash report store is required.", nameof(stores));
        }
    }

    public string ReportsPath => _stores[0].ReportsPath;

    public bool TryWrite(CrashRecord record)
        => TryWrite(store => store.TryWrite(record));

    public bool TryWriteFatal(CrashRecord record)
        => TryWrite(store => store.TryWriteFatal(record));

    public IReadOnlyList<CrashRecord> GetAll()
        => _stores
            .SelectMany(SafeGetAll)
            .GroupBy(record => record.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(record => record.OccurredUtc)
            .ThenByDescending(record => record.Id, StringComparer.Ordinal)
            .ToArray();

    public CrashRecord? Get(string id)
    {
        foreach (var store in _stores)
        {
            try
            {
                var record = store.Get(id);
                if (record is not null)
                {
                    return record;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public bool HasReportForRun(string runId)
        => _stores.Any(store =>
        {
            try
            {
                return store.HasReportForRun(runId);
            }
            catch
            {
                return false;
            }
        });

    public bool TryAcknowledge(string id)
        => TryForReport(id, store => store.TryAcknowledge(id));

    public bool TryDelete(string id)
        => TryForReport(id, store => store.TryDelete(id));

    public int DeleteAll()
    {
        var ids = GetAll().Select(record => record.Id).ToArray();
        var deleted = 0;
        foreach (var id in ids)
        {
            if (TryDelete(id))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private bool TryWrite(Func<ICrashReportStore, bool> write)
    {
        foreach (var store in _stores)
        {
            try
            {
                if (write(store))
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private bool TryForReport(string id, Func<ICrashReportStore, bool> operation)
    {
        foreach (var store in _stores)
        {
            try
            {
                if (store.Get(id) is not null && operation(store))
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static IEnumerable<CrashRecord> SafeGetAll(ICrashReportStore store)
    {
        try
        {
            return store.GetAll();
        }
        catch
        {
            return [];
        }
    }
}

internal static class AtomicFile
{
    public static void WriteText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read,
                       bufferSize: 4096,
                       options: FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
            }
        }
    }
}
