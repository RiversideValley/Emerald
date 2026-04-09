using System.Text;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emerald.CoreX.Runtime;

/// <summary>
/// Reassembles Minecraft log events from standard output and error line streams.
/// </summary>
internal sealed class MinecraftLogEventAssembler(GameLogSource source, int maxXmlPayloadLength = 64 * 1024)
{
    private sealed class PendingTextEvent(string headerLine, bool isStructured, DateTimeOffset updatedAt)
    {
        public string HeaderLine { get; } = headerLine;

        public bool IsStructured { get; } = isStructured;

        public List<string> DetailLines { get; } = [];

        public DateTimeOffset UpdatedAt { get; private set; } = updatedAt;

        public void AppendDetail(string rawLine, DateTimeOffset updatedAt)
        {
            DetailLines.Add(rawLine);
            UpdatedAt = updatedAt;
        }
    }

    private readonly StringBuilder _xmlBuffer = new();
    private PendingTextEvent? _pendingTextEvent;
    private bool _isInsideXmlEvent;
    private long _pendingTextVersion;

    private static ILogger Logger
    {
        get
        {
            try
            {
                return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(MinecraftLogEventAssembler).FullName!)
                    ?? NullLogger.Instance;
            }
            catch (InvalidOperationException)
            {
                return NullLogger.Instance;
            }
        }
    }

    public bool HasPendingText => _pendingTextEvent != null;

    public long PendingTextVersion => _pendingTextVersion;

    /// <summary>
    /// Adds a raw line to the assembler and returns any entries that became complete.
    /// </summary>
    public IReadOnlyList<GameLogEntry> AppendLine(string rawLine, DateTimeOffset now)
    {
        var finalizedEntries = new List<GameLogEntry>();

        if (_isInsideXmlEvent)
        {
            AppendXmlLine(rawLine);
            var xmlEntry = TryFinalizeXmlEvent(now);
            if (xmlEntry != null)
            {
                finalizedEntries.Add(xmlEntry);
            }

            return finalizedEntries;
        }

        if (MinecraftLogParser.IsXmlEventStart(rawLine))
        {
            FlushPendingText(finalizedEntries, now);
            AppendXmlLine(rawLine);
            var xmlEntry = TryFinalizeXmlEvent(now);
            if (xmlEntry != null)
            {
                finalizedEntries.Add(xmlEntry);
            }

            return finalizedEntries;
        }

        if (_pendingTextEvent != null && MinecraftLogParser.IsStructuredTextStart(rawLine))
        {
            FlushPendingText(finalizedEntries, now);
        }

        if (_pendingTextEvent != null)
        {
            _pendingTextEvent.AppendDetail(rawLine, now);
            _pendingTextVersion++;
            return finalizedEntries;
        }

        if (string.IsNullOrEmpty(rawLine))
        {
            return finalizedEntries;
        }

        _pendingTextEvent = new PendingTextEvent(rawLine, MinecraftLogParser.IsStructuredTextStart(rawLine), now);
        _pendingTextVersion++;
        return finalizedEntries;
    }

    /// <summary>
    /// Flushes the current pending text event only if the expected version still matches.
    /// </summary>
    public bool TryFlushPendingText(long expectedVersion, DateTimeOffset now, out GameLogEntry? entry)
    {
        entry = null;
        if (_pendingTextEvent == null || _pendingTextVersion != expectedVersion)
        {
            return false;
        }

        entry = FinalizePendingText(now);
        return entry != null;
    }

    /// <summary>
    /// Flushes any pending text event and optionally falls back incomplete XML payloads to raw text.
    /// </summary>
    public IReadOnlyList<GameLogEntry> FlushPending(DateTimeOffset now, bool includeXmlFallback)
    {
        var finalizedEntries = new List<GameLogEntry>();
        FlushPendingText(finalizedEntries, now);

        if (includeXmlFallback && _isInsideXmlEvent && _xmlBuffer.Length > 0)
        {
            Logger.LogWarning(
                "Falling back to raw payload parsing for an incomplete XML log event from {Source}. BufferedLength: {BufferedLength}.",
                source,
                _xmlBuffer.Length);
            finalizedEntries.Add(MinecraftLogParser.ParseRawPayload(_xmlBuffer.ToString(), source, now));
            ResetXmlBuffer();
        }

        return finalizedEntries;
    }

    private void FlushPendingText(List<GameLogEntry> finalizedEntries, DateTimeOffset now)
    {
        var entry = FinalizePendingText(now);
        if (entry != null)
        {
            finalizedEntries.Add(entry);
        }
    }

    private GameLogEntry? FinalizePendingText(DateTimeOffset now)
    {
        if (_pendingTextEvent == null)
        {
            return null;
        }

        var pending = _pendingTextEvent;
        _pendingTextEvent = null;

        return pending.IsStructured
            ? MinecraftLogParser.ParseTextEvent(pending.HeaderLine, pending.DetailLines, source, now)
            : MinecraftLogParser.ParseRawPayload(CombineTextPayload(pending), source, now);
    }

    private static string CombineTextPayload(PendingTextEvent pending)
    {
        var builder = new StringBuilder();
        builder.Append(pending.HeaderLine);

        foreach (var detailLine in pending.DetailLines)
        {
            builder.AppendLine();
            builder.Append(detailLine);
        }

        return builder.ToString();
    }

    private void AppendXmlLine(string rawLine)
    {
        if (_xmlBuffer.Length > 0)
        {
            _xmlBuffer.AppendLine();
        }

        _xmlBuffer.Append(rawLine);
        _isInsideXmlEvent = true;
    }

    private GameLogEntry? TryFinalizeXmlEvent(DateTimeOffset now)
    {
        if (!_isInsideXmlEvent || _xmlBuffer.Length == 0)
        {
            return null;
        }

        if (_xmlBuffer.Length > maxXmlPayloadLength)
        {
            Logger.LogWarning(
                "XML log payload from {Source} exceeded the maximum buffered length of {MaxXmlPayloadLength} characters. Falling back to raw payload parsing.",
                source,
                maxXmlPayloadLength);
            var overflowEntry = MinecraftLogParser.ParseRawPayload(_xmlBuffer.ToString(), source, now);
            ResetXmlBuffer();
            return overflowEntry;
        }

        if (!MinecraftLogParser.IsXmlEventEnd(_xmlBuffer.ToString()))
        {
            return null;
        }

        var payload = _xmlBuffer.ToString();
        ResetXmlBuffer();
        return MinecraftLogParser.ParseXmlPayload(payload, source, now);
    }

    private void ResetXmlBuffer()
    {
        _xmlBuffer.Clear();
        _isInsideXmlEvent = false;
    }
}
