using System.Diagnostics;
using Emerald.CoreX.CrashHandling;

namespace Emerald.Services;

public sealed class PlatformDiagnosticsProvider : IPlatformDiagnosticsProvider
{
    public PlatformDiagnosticsResult FindRecent(DateTimeOffset occurredUtc)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return FindRecentWindowsDump(occurredUtc);
            }

            if (OperatingSystem.IsMacOS())
            {
                return FindRecentMacReport(occurredUtc);
            }

            if (OperatingSystem.IsLinux())
            {
                return FindRecentLinuxCore(occurredUtc);
            }
        }
        catch
        {
        }

        return PlatformDiagnosticsResult.Unavailable("Platform diagnostics unavailable.");
    }

    private static PlatformDiagnosticsResult FindRecentWindowsDump(DateTimeOffset occurredUtc)
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            return PlatformDiagnosticsResult.Unavailable("Windows local dump directory is unavailable.");
        }

        var crashDumpsPath = Path.Combine(localApplicationData, "CrashDumps");
        var processName = Process.GetCurrentProcess().ProcessName;
        return FindRecentFile(
            crashDumpsPath,
            occurredUtc,
            file => file.Extension.Equals(".dmp", StringComparison.OrdinalIgnoreCase)
                    && (file.Name.StartsWith(processName, StringComparison.OrdinalIgnoreCase)
                        || file.Name.StartsWith($"{processName}.", StringComparison.OrdinalIgnoreCase)),
            "No matching Windows Error Reporting dump was found.");
    }

    private static PlatformDiagnosticsResult FindRecentMacReport(DateTimeOffset occurredUtc)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return PlatformDiagnosticsResult.Unavailable("macOS DiagnosticReports directory is unavailable.");
        }

        var reportsPath = Path.Combine(home, "Library", "Logs", "DiagnosticReports");
        var processName = Process.GetCurrentProcess().ProcessName;
        return FindRecentFile(
            reportsPath,
            occurredUtc,
            file => (file.Extension.Equals(".crash", StringComparison.OrdinalIgnoreCase)
                     || file.Extension.Equals(".ips", StringComparison.OrdinalIgnoreCase))
                    && file.Name.Contains(processName, StringComparison.OrdinalIgnoreCase),
            "No matching macOS DiagnosticReports entry was found.");
    }

    private static PlatformDiagnosticsResult FindRecentLinuxCore(DateTimeOffset occurredUtc)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new List<string>
        {
            "/var/lib/systemd/coredump"
        };

        if (!string.IsNullOrWhiteSpace(home))
        {
            candidates.Add(Path.Combine(home, ".local", "share", "systemd", "coredump"));
            candidates.Add(Path.Combine(home, ".cache", "systemd", "coredump"));
        }

        foreach (var candidate in candidates)
        {
            var result = FindRecentFile(
                candidate,
                occurredUtc,
                file => file.Name.Contains("emerald", StringComparison.OrdinalIgnoreCase)
                        || file.Name.Contains(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase),
                "No matching systemd-coredump entry was found.");
            if (result.IsAvailable)
            {
                return result;
            }
        }

        return PlatformDiagnosticsResult.Unavailable("systemd-coredump is unavailable or did not expose a matching core.");
    }

    private static PlatformDiagnosticsResult FindRecentFile(
        string directory,
        DateTimeOffset occurredUtc,
        Func<FileInfo, bool> predicate,
        string unavailableMessage)
    {
        if (!Directory.Exists(directory))
        {
            return PlatformDiagnosticsResult.Unavailable(unavailableMessage);
        }

        var lowerBound = occurredUtc.UtcDateTime.AddMinutes(-10);
        var upperBound = occurredUtc.UtcDateTime.AddMinutes(5);
        var file = new DirectoryInfo(directory)
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(predicate)
            .Where(candidate => candidate.LastWriteTimeUtc >= lowerBound)
            .Where(candidate => candidate.LastWriteTimeUtc <= upperBound)
            .OrderByDescending(candidate => candidate.LastWriteTimeUtc)
            .FirstOrDefault();

        return file is null
            ? PlatformDiagnosticsResult.Unavailable(unavailableMessage)
            : new PlatformDiagnosticsResult("Available", file.FullName);
    }
}
