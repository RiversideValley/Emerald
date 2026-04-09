namespace Emerald.CoreX.Runtime;

internal readonly record struct GameLogDeduplicationResult(bool ShouldAppend, GameLogEntry? EntryToRemove = null);

internal sealed class GameLogDeduplicator(TimeSpan? dedupeWindow = null)
{
    private sealed class RecentEntryRecord
    {
        public required GameLogEntry Entry { get; set; }
        public required DateTimeOffset SeenAt { get; set; }
    }

    private readonly TimeSpan _dedupeWindow = dedupeWindow ?? TimeSpan.FromSeconds(2);
    private readonly Dictionary<string, List<RecentEntryRecord>> _recentEntries = new(StringComparer.Ordinal);

    public GameLogDeduplicationResult Register(GameLogEntry entry, DateTimeOffset seenAt)
    {
        if (entry.Source is GameLogSource.Lifecycle or GameLogSource.CrashReport)
        {
            return new GameLogDeduplicationResult(true);
        }

        PruneStaleEntries(seenAt);

        var fingerprint = BuildFingerprint(entry);
        if (_recentEntries.TryGetValue(fingerprint, out var existingEntries))
        {
            foreach (var existing in existingEntries)
            {
                if (seenAt - existing.SeenAt > _dedupeWindow || !AreMetadataCompatible(existing.Entry, entry))
                {
                    continue;
                }

                existing.SeenAt = seenAt;

                if (ShouldPreferIncoming(entry, existing.Entry))
                {
                    var replacedEntry = existing.Entry;
                    existing.Entry = entry;
                    return new GameLogDeduplicationResult(true, replacedEntry);
                }

                return new GameLogDeduplicationResult(false);
            }
        }

        if (!_recentEntries.TryGetValue(fingerprint, out existingEntries))
        {
            existingEntries = [];
            _recentEntries[fingerprint] = existingEntries;
        }

        existingEntries.Add(new RecentEntryRecord
        {
            Entry = entry,
            SeenAt = seenAt
        });

        return new GameLogDeduplicationResult(true);
    }

    private void PruneStaleEntries(DateTimeOffset now)
    {
        var cutoff = now - TimeSpan.FromSeconds(10);
        var staleKeys = new List<string>();

        foreach (var recentEntry in _recentEntries)
        {
            recentEntry.Value.RemoveAll(x => x.SeenAt < cutoff);
            if (recentEntry.Value.Count == 0)
            {
                staleKeys.Add(recentEntry.Key);
            }
        }

        foreach (var staleKey in staleKeys)
        {
            _recentEntries.Remove(staleKey);
        }
    }

    private static string BuildFingerprint(GameLogEntry entry)
    {
        var timestampBucket = !string.IsNullOrWhiteSpace(entry.OriginalTimeText)
            ? entry.OriginalTimeText!.Trim()
            : entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");

        return string.Join("|",
            timestampBucket,
            entry.LevelText,
            NormalizeText(entry.Message),
            NormalizeText(entry.DetailsText));
    }

    private static bool AreMetadataCompatible(GameLogEntry existing, GameLogEntry incoming)
        => AreCompatible(existing.ThreadName, incoming.ThreadName)
            && AreCompatible(existing.LoggerName, incoming.LoggerName);

    private static bool AreCompatible(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
    }

    private static bool ShouldPreferIncoming(GameLogEntry incoming, GameLogEntry existing)
    {
        var sourcePreference = GetSourcePreference(incoming.Source).CompareTo(GetSourcePreference(existing.Source));
        if (sourcePreference != 0)
        {
            return sourcePreference > 0;
        }

        var richnessPreference = GetRichnessScore(incoming).CompareTo(GetRichnessScore(existing));
        if (richnessPreference != 0)
        {
            return richnessPreference > 0;
        }

        return false;
    }

    private static int GetSourcePreference(GameLogSource source) => source switch
    {
        GameLogSource.FileTail => 3,
        GameLogSource.StandardOutput => 2,
        GameLogSource.StandardError => 1,
        _ => 0
    };

    private static int GetRichnessScore(GameLogEntry entry)
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(entry.ThreadName))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(entry.LoggerName))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(entry.DetailsText))
        {
            score++;
        }

        if (HasPreciseXmlPayload(entry))
        {
            score++;
        }

        return score;
    }

    private static bool HasPreciseXmlPayload(GameLogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RawPayload))
        {
            return false;
        }

        var trimmed = entry.RawPayload.TrimStart();
        return trimmed.StartsWith("<log4j:Event", StringComparison.Ordinal)
            || trimmed.StartsWith("<Event", StringComparison.Ordinal);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None)
            .Select(line => line.Trim());

        return string.Join("\n", lines).Trim();
    }
}
