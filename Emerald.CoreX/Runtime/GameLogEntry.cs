using System.Text;

namespace Emerald.CoreX.Runtime;

public sealed class GameLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }

    public string? OriginalTimeText { get; init; }

    public required GameLogLevel Level { get; init; }

    public required string Message { get; init; }

    public string? DetailsText { get; init; }

    public string? ThreadName { get; init; }

    public string? LoggerName { get; init; }

    public required GameLogSource Source { get; init; }

    public string? RawPayload { get; init; }

    public bool IsSynthetic { get; init; }

    public bool HasDetails => !string.IsNullOrWhiteSpace(DetailsText);

    public string TimestampText => OriginalTimeText ?? Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string LevelText => Level switch
    {
        GameLogLevel.Warn => "WARN",
        GameLogLevel.Error => "ERROR",
        GameLogLevel.Fatal => "FATAL",
        GameLogLevel.Debug => "DEBUG",
        GameLogLevel.Trace => "TRACE",
        GameLogLevel.Info => "INFO",
        _ => "LOG"
    };

    public string? MetadataText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ThreadName))
            {
                parts.Add(ThreadName);
            }

            if (!string.IsNullOrWhiteSpace(LoggerName))
            {
                parts.Add(LoggerName);
            }

            return parts.Count == 0 ? null : string.Join(" / ", parts);
        }
    }

    public string ToClipboardText()
    {
        var builder = new StringBuilder();
        builder.Append('[').Append(TimestampText).Append("] [").Append(LevelText).Append(']');

        if (!string.IsNullOrWhiteSpace(MetadataText))
        {
            builder.Append(" [").Append(MetadataText).Append(']');
        }

        builder.Append(' ').Append(Message);

        if (HasDetails)
        {
            builder.AppendLine();
            builder.Append(DetailsText);
        }

        return builder.ToString();
    }
}
