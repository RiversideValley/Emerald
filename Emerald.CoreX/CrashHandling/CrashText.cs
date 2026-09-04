using System.Text;
using System.Text.RegularExpressions;

namespace Emerald.CoreX.CrashHandling;

public static class CrashTextSanitizer
{
    private const int DefaultMaxLength = 64 * 1024;

    private static readonly Regex SecretRegex = new(
        """(?ix)(?<key>["']?(?:authorization|access[_-]?token|refresh[_-]?token|id[_-]?token|token|client[_-]?secret|api[_-]?(?:key|secret)|private[_-]?key|password|passwd|secret|cookie|credential|username|email)["']?)(?<separator>\s*[:=]\s*(?:(?:bearer|basic)\s+)?|\s+(?:bearer|basic)\s+|\s+)(?<value>"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^\s,;&}]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UnixUserPathRegex = new(
        @"(?i)(?:/Users/|/home/)[^/\s]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WindowsUserPathRegex = new(
        @"(?i)[A-Z]:\\Users\\[^\s\\]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Sanitize(string? value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value.Replace("\0", string.Empty, StringComparison.Ordinal);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            sanitized = sanitized.Replace(home, "<home>", StringComparison.OrdinalIgnoreCase);
        }

        sanitized = UnixUserPathRegex.Replace(sanitized, "/<home>");
        sanitized = WindowsUserPathRegex.Replace(sanitized, "<home>");
        sanitized = SecretRegex.Replace(sanitized, match =>
            match.Groups["key"].Value + ": [REDACTED]");

        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return sanitized[..Math.Max(0, maxLength - 32)] + "\n[TRUNCATED]";
    }
}

public static class CrashLogTailReader
{
    public static string Read(string? path, int maxBytes = 64 * 1024)
    {
        if (string.IsNullOrWhiteSpace(path) || maxBytes <= 0)
        {
            return string.Empty;
        }

        try
        {
            var readablePath = ResolvePath(path);
            if (readablePath is null)
            {
                return string.Empty;
            }

            using var stream = File.Open(readablePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var start = Math.Max(0, stream.Length - maxBytes);
            stream.Seek(start, SeekOrigin.Begin);
            var bufferLength = (int)Math.Min(maxBytes, Math.Max(0, stream.Length - start));
            var buffer = new byte[bufferLength];
            var read = stream.Read(buffer, 0, buffer.Length);
            return CrashTextSanitizer.Sanitize(Encoding.UTF8.GetString(buffer, 0, read));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? ResolvePath(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        var prefix = Path.GetFileNameWithoutExtension(fileName);
        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles($"{prefix}*{extension}", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch
        {
            return null;
        }
    }
}

public static class CrashReportFormatter
{
    public static string ToText(CrashRecord record)
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== EMERALD CRASH REPORT ===");
        builder.AppendLine($"Report ID: {record.Id}");
        builder.AppendLine($"Run ID: {record.RunId}");
        builder.AppendLine($"Type: {record.Kind}");
        builder.AppendLine($"Occurred (UTC): {record.OccurredUtc:O}");
        builder.AppendLine($"Source: {CrashTextSanitizer.Sanitize(record.Source, 512)}");
        builder.AppendLine();
        builder.AppendLine("=== ENVIRONMENT ===");
        builder.AppendLine($"App version: {CrashTextSanitizer.Sanitize(record.AppVersion, 512)}");
        builder.AppendLine($"Package version: {CrashTextSanitizer.Sanitize(record.PackageVersion, 512)}");
        builder.AppendLine($"Build channel: {CrashTextSanitizer.Sanitize(record.BuildChannel, 512)}");
        builder.AppendLine($"Release tag: {CrashTextSanitizer.Sanitize(record.ReleaseTag, 512)}");
        builder.AppendLine($"Commit: {CrashTextSanitizer.Sanitize(record.CommitSha, 512)}");
        builder.AppendLine($"Build timestamp: {CrashTextSanitizer.Sanitize(record.BuildTimestampUtc, 512)}");
        builder.AppendLine($"Platform: {CrashTextSanitizer.Sanitize(record.Platform, 512)}");
        builder.AppendLine($"Operating system: {CrashTextSanitizer.Sanitize(record.OperatingSystem, 1024)}");
        builder.AppendLine($"Architecture: {CrashTextSanitizer.Sanitize(record.Architecture, 256)}");
        builder.AppendLine($"Runtime: {CrashTextSanitizer.Sanitize(record.Runtime, 512)}");
        builder.AppendLine($"Native diagnostics: {CrashTextSanitizer.Sanitize(record.NativeDiagnosticsStatus, 512)}");
        if (!string.IsNullOrWhiteSpace(record.ReportPath))
        {
            builder.AppendLine($"Local report: {CrashTextSanitizer.Sanitize(record.ReportPath, 2048)}");
        }
        if (!string.IsNullOrWhiteSpace(record.NativeDiagnosticsPath))
        {
            builder.AppendLine($"Native diagnostics path: {CrashTextSanitizer.Sanitize(record.NativeDiagnosticsPath, 2048)}");
        }

        if (record.Exception is not null)
        {
            builder.AppendLine();
            builder.AppendLine("=== EXCEPTION ===");
            AppendException(builder, record.Exception, 0);
        }

        if (!string.IsNullOrWhiteSpace(record.ApplicationLogTail))
        {
            builder.AppendLine();
            builder.AppendLine("=== APPLICATION LOG TAIL ===");
            builder.AppendLine(CrashTextSanitizer.Sanitize(record.ApplicationLogTail));
        }

        return builder.ToString();
    }

    public static string ToGitHubSummary(CrashRecord record)
    {
        var exception = record.Exception;
        var builder = new StringBuilder();
        builder.AppendLine("## Emerald crash report");
        builder.AppendLine();
        builder.AppendLine("The full sanitized report has been copied to the clipboard by Emerald.");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Report ID | {record.Id} |");
        builder.AppendLine($"| Type | {record.Kind} |");
        builder.AppendLine($"| Version | {EscapeTable(record.AppVersion)} |");
        builder.AppendLine($"| Platform | {EscapeTable(record.Platform)} |");
        builder.AppendLine($"| Source | {EscapeTable(record.Source)} |");
        builder.AppendLine($"| Exception | {EscapeTable(exception?.Type ?? "Unavailable")} |");
        builder.AppendLine();
        builder.AppendLine("Please paste the copied report below and describe what Emerald was doing before the crash.");
        return builder.ToString();
    }

    public static string ToGitHubTitle(CrashRecord record)
        => $"Emerald {record.Kind.ToString().ToLowerInvariant()} - "
           + $"{CrashTextSanitizer.Sanitize(record.AppVersion, 128)} - "
           + CrashTextSanitizer.Sanitize(record.Platform, 128);

    private static void AppendException(StringBuilder builder, CrashExceptionInfo exception, int depth)
    {
        var indent = new string(' ', depth * 2);
        builder.AppendLine($"{indent}Type: {CrashTextSanitizer.Sanitize(exception.Type, 1024)}");
        builder.AppendLine($"{indent}Message: {CrashTextSanitizer.Sanitize(exception.Message, 16_384)}");
        builder.AppendLine($"{indent}HResult: {exception.HResult?.ToString() ?? "Unavailable"}");
        builder.AppendLine($"{indent}Stack trace:");
        builder.AppendLine(CrashTextSanitizer.Sanitize(exception.StackTrace, 32_768));

        foreach (var inner in exception.InnerExceptions)
        {
            builder.AppendLine($"{indent}Inner exception:");
            AppendException(builder, inner, depth + 1);
        }
    }

    private static string EscapeTable(string value)
        => CrashTextSanitizer.Sanitize(value, 256)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace(((char)96).ToString(), "'", StringComparison.Ordinal);
}

public sealed record GitHubIssueDraft(string Url, string Title, string Body, string FullReport);

public sealed class GitHubCrashIssueComposer
{
    private const int MaximumEncodedUrlLength = 2_000;
    private readonly string _repositoryUrl;

    public GitHubCrashIssueComposer(string repositoryUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        _repositoryUrl = repositoryUrl.TrimEnd('/');
    }

    public GitHubIssueDraft Compose(CrashRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var title = CrashReportFormatter.ToGitHubTitle(record);
        var body = CrashReportFormatter.ToGitHubSummary(record);
        var url = ComposeUrl(title, body);
        return new GitHubIssueDraft(url, title, body, CrashReportFormatter.ToText(record));
    }

    private string ComposeUrl(string title, string body)
    {
        var url = BuildUrl(title, body);
        if (url.Length <= MaximumEncodedUrlLength)
        {
            return url;
        }

        var compactBody = "## Emerald crash report\n\n"
            + "Report ID: " + CrashTextSanitizer.Sanitize(title, 128) + "\n"
            + "Please paste the complete sanitized report from the clipboard.";
        url = BuildUrl(CrashTextSanitizer.Sanitize(title, 96), compactBody);
        if (url.Length <= MaximumEncodedUrlLength)
        {
            return url;
        }

        return BuildUrl("Emerald crash report", "Please paste the complete sanitized report from the clipboard.");
    }

    private string BuildUrl(string title, string body)
        => $"{_repositoryUrl}/issues/new?template=crash_report.md&title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
}
