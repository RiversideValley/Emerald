namespace Emerald.Models;

public enum AppReleaseChannel
{
    Nightly,
    Prerelease,
    Release
}

public static class AppReleaseChannelExtensions
{
    public static AppReleaseChannel Parse(string? rawValue, AppReleaseChannel fallback = AppReleaseChannel.Nightly)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        return rawValue.Trim().ToLowerInvariant() switch
        {
            "nightly" => AppReleaseChannel.Nightly,
            "pre-release" => AppReleaseChannel.Prerelease,
            "prerelease" => AppReleaseChannel.Prerelease,
            "pre" => AppReleaseChannel.Prerelease,
            "release" => AppReleaseChannel.Release,
            _ => fallback
        };
    }

    public static string ToMetadataValue(this AppReleaseChannel channel)
    {
        return channel switch
        {
            AppReleaseChannel.Nightly => "nightly",
            AppReleaseChannel.Prerelease => "pre-release",
            _ => "release"
        };
    }
}
