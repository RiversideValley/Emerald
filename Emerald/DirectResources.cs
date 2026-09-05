using Emerald.Helpers;
using Emerald.Models;
using Emerald.Services;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.UI;

namespace Emerald;

public static class DirectResoucres
{
    private const string ChannelMetadataKey = "Emerald.UpdateChannel";
    private const string PublicVersionMetadataKey = "Emerald.PublicVersion";
    private const string ReleaseTagMetadataKey = "Emerald.ReleaseTag";
    private const string CommitMetadataKey = "Emerald.CommitSha";
    private const string TimestampMetadataKey = "Emerald.BuildTimestampUtc";

    private static readonly Assembly EntryAssembly = Assembly.GetExecutingAssembly();
    private static readonly IReadOnlyDictionary<string, string> AssemblyMetadata = LoadAssemblyMetadata();

    public static int MaxRAM
        => (DeviceInfoHelper.GetMemoryGB() ?? 192) * 1024; //switches PC ram if failed, I couldn't find the max ram for MC.

    public static int MinRAM
        => 512;

    //Used this thing for major setting changes because older settings could crash the program
    public static string SettingsAPIVersion
        => "1.3";

    public static string Platform
    {
        get
        {
           if( OperatingSystem.IsWindows())
            return "Windows";
            else if (OperatingSystem.IsLinux())
                return "Linux";
            else if (OperatingSystem.IsMacOS())
                return "OSX";
           else
                return "Unknown";
        }
    }
    public static string LocalDataPath
    {
        get
        {
#if DEBUG
            // Process-level crash tests must not touch a developer's real profile.
            if (CrashFaultInjection.IsEnabled)
            {
                var testDataRoot = Environment.GetEnvironmentVariable("EMERALD_TEST_DATA_ROOT");
                if (!string.IsNullOrWhiteSpace(testDataRoot))
                {
                    return testDataRoot;
                }
            }
#endif
#if WINDOWS
            return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald");
#endif
        }
    }

    public static string SafeLocalDataPath
    {
        get
        {
#if DEBUG
            if (CrashFaultInjection.IsEnabled)
            {
                var testDataRoot = Environment.GetEnvironmentVariable("EMERALD_TEST_DATA_ROOT");
                if (!string.IsNullOrWhiteSpace(testDataRoot))
                {
                    return testDataRoot;
                }
            }
#endif
            try
            {
                if (!string.IsNullOrWhiteSpace(LocalDataPath))
                {
                    return LocalDataPath;
                }
            }
            catch
            {
            }

            try
            {
                var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrWhiteSpace(localApplicationData))
                {
                    return Path.Combine(localApplicationData, "Emerald");
                }
            }
            catch
            {
            }

            return Path.Combine(Path.GetTempPath(), "Emerald");
        }
    }
    public static string BuildType
    {
        get
        {
#if DEBUG
            return "DEBUG";
#else
            return "RELEASE";
#endif
        }
    }
    public static Architecture Architecture => RuntimeInformation.ProcessArchitecture;

    public static string AppVersion
        => PublicVersion;

    public static string PublicVersion
        => GetAssemblyMetadata(PublicVersionMetadataKey, EntryAssembly.GetName().Version?.ToString() ?? "0.0.0.0");

    public static string PackageVersion
        => EntryAssembly.GetName().Version?.ToString() ?? "0.0.0.0";

    public static AppReleaseChannel ReleaseChannel
        => AppReleaseChannelExtensions.Parse(GetAssemblyMetadata(ChannelMetadataKey, null));

    public static string ReleaseTag
        => GetAssemblyMetadata(ReleaseTagMetadataKey, string.Empty);

    public static string CommitSha
        => GetAssemblyMetadata(CommitMetadataKey, string.Empty);

    public static string BuildTimestampUtc
        => GetAssemblyMetadata(TimestampMetadataKey, string.Empty);

    public static Color LayerFillColorDefaultColor
        => (Color)App.Current.Resources["LayerFillColorDefault"];

    private static IReadOnlyDictionary<string, string> LoadAssemblyMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in EntryAssembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (string.IsNullOrWhiteSpace(attribute.Key))
            {
                continue;
            }

            metadata[attribute.Key] = attribute.Value ?? string.Empty;
        }

        return metadata;
    }

    private static string GetAssemblyMetadata(string key, string? fallback)
    {
        return AssemblyMetadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback ?? string.Empty;
    }
}
