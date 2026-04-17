using Emerald.Helpers;
using Emerald.Models;
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
        => (DeviceInfoHelper.GetMemoryGB() ?? 192) * 1024; //switches maximum ram if failed, I couldn't find the max ram for MC.

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
#if WINDOWS
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald");
#endif
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
