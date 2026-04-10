using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emerald.CoreX.Runtime;

/// <summary>
/// Parses raw Minecraft process output into normalized runtime log entries.
/// </summary>
internal static partial class MinecraftLogParser
{
    private static readonly Regex StructuredLineRegex = StructuredLine();
    private const string Log4jEventsNamespace = "http://logging.apache.org/log4j/2.0/events";
    private static ILogger Logger
    {
        get
        {
            try
            {
                return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(MinecraftLogParser).FullName!)
                    ?? NullLogger.Instance;
            }
            catch (InvalidOperationException)
            {
                return NullLogger.Instance;
            }
        }
    }

    /// <summary>
    /// Determines whether a raw line starts a structured text log event.
    /// </summary>
    public static bool IsStructuredTextStart(string rawLine)
        => !string.IsNullOrEmpty(rawLine) && StructuredLineRegex.IsMatch(rawLine);

    /// <summary>
    /// Determines whether a raw line starts a log4j XML event payload.
    /// </summary>
    public static bool IsXmlEventStart(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        var trimmed = rawLine.TrimStart();
        return trimmed.StartsWith("<log4j:Event", StringComparison.Ordinal)
            || trimmed.StartsWith("<Event", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the supplied payload contains the closing tag for an XML event.
    /// </summary>
    public static bool IsXmlEventEnd(string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return false;
        }

        return rawPayload.Contains("</log4j:Event>", StringComparison.Ordinal)
            || rawPayload.Contains("</Event>", StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses a structured text event and falls back to raw payload parsing when needed.
    /// </summary>
    public static GameLogEntry ParseTextEvent(string headerLine, IReadOnlyList<string> detailLines, GameLogSource source, DateTimeOffset finalizedAt)
    {
        var structuredMatch = StructuredLineRegex.Match(headerLine);
        if (!structuredMatch.Success)
        {
            Logger.LogDebug(
                "Structured text log header did not match the expected Minecraft format for {Source}. Falling back to raw payload parsing.",
                source);
            return ParseRawPayload(CombinePayload(headerLine, detailLines), source, finalizedAt);
        }

        return new GameLogEntry
        {
            Timestamp = finalizedAt,
            OriginalTimeText = structuredMatch.Groups["time"].Value,
            Level = ParseLevel(structuredMatch.Groups["level"].Value),
            Message = structuredMatch.Groups["message"].Value,
            DetailsText = JoinDetails(detailLines),
            ThreadName = structuredMatch.Groups["thread"].Value,
            LoggerName = structuredMatch.Groups["logger"].Success ? structuredMatch.Groups["logger"].Value : null,
            Source = source,
            RawPayload = CombinePayload(headerLine, detailLines)
        };
    }

    /// <summary>
    /// Parses an unstructured payload into a basic runtime log entry.
    /// </summary>
    public static GameLogEntry ParseRawPayload(string rawPayload, GameLogSource source, DateTimeOffset finalizedAt)
    {
        var lines = rawPayload.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var message = lines.FirstOrDefault() ?? string.Empty;
        var details = lines.Length > 1 ? string.Join(Environment.NewLine, lines.Skip(1)) : null;

        return new GameLogEntry
        {
            Timestamp = finalizedAt,
            Level = source == GameLogSource.StandardError ? GameLogLevel.Error : GameLogLevel.Unknown,
            Message = message,
            DetailsText = details,
            Source = source,
            RawPayload = rawPayload
        };
    }

    /// <summary>
    /// Parses a log4j XML payload and falls back to raw parsing when the payload is incomplete or malformed.
    /// </summary>
    public static GameLogEntry ParseXmlPayload(string rawPayload, GameLogSource source, DateTimeOffset fallbackTimestamp)
    {
        try
        {
            var document = XDocument.Parse(
                $"""<Root xmlns:log4j="{Log4jEventsNamespace}">{rawPayload}</Root>""",
                LoadOptions.PreserveWhitespace);

            var eventElement = document.Root?.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "Event", StringComparison.Ordinal));
            if (eventElement == null)
            {
                Logger.LogDebug(
                    "XML log payload from {Source} did not contain a log event element. Falling back to raw payload parsing.",
                    source);
                return ParseRawPayload(rawPayload, source, fallbackTimestamp);
            }

            var timestamp = ParseTimestamp(eventElement, fallbackTimestamp);
            var message = GetDescendantValue(eventElement, "Message");
            var throwable = TrimOuterLineBreaks(GetDescendantValue(eventElement, "Throwable"));

            return new GameLogEntry
            {
                Timestamp = timestamp,
                Level = ParseLevel(GetAttributeValue(eventElement, "level")),
                Message = string.IsNullOrWhiteSpace(message) ? eventElement.ToString(SaveOptions.DisableFormatting) : message,
                DetailsText = throwable,
                ThreadName = GetAttributeValue(eventElement, "thread"),
                LoggerName = GetAttributeValue(eventElement, "logger") ?? GetAttributeValue(eventElement, "loggerName"),
                Source = source,
                RawPayload = rawPayload
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Failed to parse XML log payload from {Source}. PayloadLength: {PayloadLength}. Falling back to raw payload parsing.",
                source,
                rawPayload.Length);
            return ParseRawPayload(rawPayload, source, fallbackTimestamp);
        }
    }

    private static string CombinePayload(string headerLine, IReadOnlyList<string> detailLines)
    {
        if (detailLines.Count == 0)
        {
            return headerLine;
        }

        var builder = new StringBuilder(headerLine);
        foreach (var detailLine in detailLines)
        {
            builder.AppendLine();
            builder.Append(detailLine);
        }

        return builder.ToString();
    }

    private static string? JoinDetails(IReadOnlyList<string> detailLines)
        => detailLines.Count == 0 ? null : string.Join(Environment.NewLine, detailLines);

    private static DateTimeOffset ParseTimestamp(XElement eventElement, DateTimeOffset fallbackTimestamp)
    {
        var timestampValue = GetAttributeValue(eventElement, "timestamp") ?? GetAttributeValue(eventElement, "timeMillis");
        if (!string.IsNullOrWhiteSpace(timestampValue))
        {
            if (long.TryParse(timestampValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMillis))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(epochMillis);
            }

            if (DateTimeOffset.TryParse(timestampValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedTimestamp))
            {
                return parsedTimestamp;
            }
        }

        var instantElement = eventElement.Elements().FirstOrDefault(x => string.Equals(x.Name.LocalName, "Instant", StringComparison.Ordinal));
        if (instantElement != null)
        {
            var epochSecondValue = GetAttributeValue(instantElement, "epochSecond");
            var nanoValue = GetAttributeValue(instantElement, "nanoOfSecond");

            if (long.TryParse(epochSecondValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochSecond))
            {
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(epochSecond);
                if (int.TryParse(nanoValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nanos))
                {
                    timestamp = timestamp.AddTicks(nanos / 100);
                }

                return timestamp;
            }
        }

        return fallbackTimestamp;
    }

    private static string? GetAttributeValue(XElement element, string localName)
        => element.Attributes().FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? GetDescendantValue(XElement element, string localName)
        => element.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? TrimOuterLineBreaks(string? value)
        => string.IsNullOrEmpty(value) ? value : value.Trim('\r', '\n');

    /// <summary>
    /// Normalizes a raw level token into the runtime log level enum.
    /// </summary>
    internal static GameLogLevel ParseLevel(string? value) => value?.ToUpperInvariant() switch
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
