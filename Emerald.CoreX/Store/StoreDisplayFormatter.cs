using System.Globalization;

namespace Emerald.CoreX.Store;

internal static class StoreDisplayFormatter
{
    private static readonly Dictionary<string, string> SpecialDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["datapack"] = "Data Pack",
        ["resourcepack"] = "Resource Pack",
        ["game-mechanics"] = "Game Mechanics",
        ["neoforge"] = "NeoForge",
        ["optifine"] = "OptiFine",
        ["liteloader"] = "LiteLoader",
        ["bungeecord"] = "BungeeCord"
    };

    public static string ToDisplayLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (SpecialDisplayNames.TryGetValue(value, out var special))
        {
            return special;
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        var words = value
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => textInfo.ToTitleCase(word.ToLowerInvariant()));

        return string.Join(" ", words);
    }

    public static string FormatRelativeTime(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        var delta = DateTime.UtcNow - utc;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta < TimeSpan.FromMinutes(1))
        {
            return "Just now";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, delta.Minutes)}m ago";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return $"{Math.Max(1, delta.Hours)}h ago";
        }

        if (delta < TimeSpan.FromDays(7))
        {
            return $"{Math.Max(1, delta.Days)}d ago";
        }

        if (delta < TimeSpan.FromDays(30))
        {
            return $"{Math.Max(1, delta.Days / 7)}w ago";
        }

        if (delta < TimeSpan.FromDays(365))
        {
            return $"{Math.Max(1, delta.Days / 30)}mo ago";
        }

        return $"{Math.Max(1, delta.Days / 365)}y ago";
    }

    public static string FormatFileSize(long? sizeBytes)
    {
        if (sizeBytes is null or < 0)
        {
            return string.Empty;
        }

        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)sizeBytes.Value;
        var suffixIndex = 0;
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{size:0} {suffixes[suffixIndex]}"
            : $"{size:0.#} {suffixes[suffixIndex]}";
    }

    public static string FormatCompatibility(string? clientSide, string? serverSide)
    {
        var client = NormalizeCompatibilityValue(clientSide);
        var server = NormalizeCompatibilityValue(serverSide);

        return (client, server) switch
        {
            ("required", "required") => "Client & server",
            ("required", "optional") => "Client & server",
            ("optional", "required") => "Client & server",
            ("required", "unsupported") => "Client",
            ("optional", "unsupported") => "Client",
            ("unsupported", "required") => "Server",
            ("unsupported", "optional") => "Server",
            ("optional", "optional") => "Client or server",
            _ => "Compatibility unknown"
        };
    }

    public static string FormatContentType(StoreContentType contentType)
    {
        return contentType switch
        {
            StoreContentType.Mod => "Mod",
            StoreContentType.ResourcePack => "Resource Pack",
            StoreContentType.DataPack => "Data Pack",
            StoreContentType.Shader => "Shader",
            StoreContentType.Plugin => "Plugin",
            _ => contentType.ToString()
        };
    }

    private static string NormalizeCompatibilityValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
}
