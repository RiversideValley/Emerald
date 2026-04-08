using System.Text.RegularExpressions;

namespace Emerald.CoreX.Runtime;

internal static partial class MinecraftLogParser
{
    private static readonly Regex StructuredLineRegex = StructuredLine();

    public static GameLogEntry Parse(string rawLine, GameLogSource source, GameLogEntry? previousEntry)
    {
        if (previousEntry != null && IsContinuationLine(rawLine))
        {
            return new GameLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = previousEntry.Level,
                Message = rawLine,
                ThreadName = previousEntry.ThreadName,
                LoggerName = previousEntry.LoggerName,
                Source = source,
                RawLine = rawLine,
                IsContinuation = true
            };
        }

        var structuredMatch = StructuredLineRegex.Match(rawLine);
        if (structuredMatch.Success)
        {
            return new GameLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                OriginalTimeText = structuredMatch.Groups["time"].Value,
                Level = ParseLevel(structuredMatch.Groups["level"].Value),
                Message = structuredMatch.Groups["message"].Value,
                ThreadName = structuredMatch.Groups["thread"].Value,
                LoggerName = structuredMatch.Groups["logger"].Success ? structuredMatch.Groups["logger"].Value : null,
                Source = source,
                RawLine = rawLine
            };
        }

        return new GameLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = source == GameLogSource.StandardError ? GameLogLevel.Error : GameLogLevel.Unknown,
            Message = rawLine,
            Source = source,
            RawLine = rawLine
        };
    }

    private static bool IsContinuationLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        return rawLine.StartsWith('\t')
            || rawLine.StartsWith("    ", StringComparison.Ordinal)
            || rawLine.StartsWith("at ", StringComparison.Ordinal)
            || rawLine.StartsWith("... ", StringComparison.Ordinal)
            || rawLine.StartsWith("Caused by:", StringComparison.OrdinalIgnoreCase)
            || rawLine.StartsWith("Suppressed:", StringComparison.OrdinalIgnoreCase);
    }

    private static GameLogLevel ParseLevel(string value) => value.ToUpperInvariant() switch
    {
        "TRACE" => GameLogLevel.Trace,
        "DEBUG" => GameLogLevel.Debug,
        "INFO" => GameLogLevel.Info,
        "WARN" => GameLogLevel.Warn,
        "ERROR" => GameLogLevel.Error,
        "FATAL" => GameLogLevel.Fatal,
        _ => GameLogLevel.Unknown
    };

    [GeneratedRegex("^\\[(?<time>\\d{2}:\\d{2}:\\d{2})\\] \\[(?<thread>.+?)/(?<level>TRACE|DEBUG|INFO|WARN|ERROR|FATAL)(?:/(?<logger>[^\\]]+))?\\]: (?<message>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StructuredLine();
}
