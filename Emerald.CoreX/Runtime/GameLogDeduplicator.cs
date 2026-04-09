namespace Emerald.CoreX.Runtime;

internal readonly record struct GameLogDeduplicationResult(bool ShouldAppend, GameLogEntry? EntryToRemove = null);

internal sealed class GameLogDeduplicator(TimeSpan? dedupeWindow = null)
{
    private sealed class RecentEntryRecord
    {
        public required GameLogEntry Entry { get; init; }
        public required DateTimeOffset SeenAt { get; set; }
    }

    private readonly TimeSpan _dedupeWindow = dedupeWindow ?? TimeSpan.FromSeconds(2);
    private readonly Dictionary<string, RecentEntryRecord> _recentEntries = new(StringComparer.Ordinal);

    public GameLogDeduplicationResult Register(GameLogEntry entry, DateTimeOffset seenAt)
    {
        if (entry.Source is GameLogSource.Lifecycle or GameLogSource.CrashReport)
        {
            return new GameLogDeduplicationResult(true);
        }

        PruneStaleEntries(seenAt);

        var fingerprint = BuildFingerprint(entry);
        if (_recentEntries.TryGetValue(fingerprint, out var existing)
            && seenAt - existing.SeenAt <= _dedupeWindow)
        {
            existing.SeenAt = seenAt;

            if (entry.Source == GameLogSource.FileTail && existing.Entry.Source != GameLogSource.FileTail)
            {
                var replacedEntry = existing.Entry;
                _recentEntries[fingerprint] = new RecentEntryRecord
                {
                    Entry = entry,
                    SeenAt = seenAt
                };

                return new GameLogDeduplicationResult(true, replacedEntry);
            }

            if (existing.Entry.Source == GameLogSource.FileTail && entry.Source != GameLogSource.FileTail)
            {
                return new GameLogDeduplicationResult(false);
            }

            return new GameLogDeduplicationResult(false);
        }

        _recentEntries[fingerprint] = new RecentEntryRecord
        {
            Entry = entry,
            SeenAt = seenAt
        };

        return new GameLogDeduplicationResult(true);
    }

    private void PruneStaleEntries(DateTimeOffset now)
    {
        if (_recentEntries.Count <= 256)
        {
            return;
        }

        var cutoff = now - TimeSpan.FromSeconds(10);
        var staleKeys = _recentEntries
            .Where(x => x.Value.SeenAt < cutoff)
            .Select(x => x.Key)
            .ToArray();

        foreach (var staleKey in staleKeys)
        {
            _recentEntries.Remove(staleKey);
        }
    }

    private static string BuildFingerprint(GameLogEntry entry)
    {
        var payload = !string.IsNullOrWhiteSpace(entry.RawPayload)
            ? entry.RawPayload!
            : $"{entry.TimestampText}|{entry.LevelText}|{entry.ThreadName}|{entry.LoggerName}|{entry.Message}|{entry.DetailsText}";

        return payload.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }
}
