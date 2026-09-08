using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Runtime;

public sealed record MinecraftLaunchCapabilities(bool CanLaunchServer, bool CanQuickPlayWorld, string? WorldLaunchUnavailableReason = null);

public interface IMinecraftLaunchCapabilityResolver
{
    Task<MinecraftLaunchCapabilities> ResolveAsync(Game game, CancellationToken cancellationToken = default);
}

public sealed class MinecraftLaunchCapabilityResolver(ILogger<MinecraftLaunchCapabilityResolver> logger) : IMinecraftLaunchCapabilityResolver
{
    private readonly ConcurrentDictionary<string, MinecraftLaunchCapabilities> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<MinecraftLaunchCapabilities> ResolveAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var versionId = game.Version.RealVersion ?? game.Version.BasedOn;
        var key = $"{game.Path.Versions}|{versionId}";
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var resolved = await InspectAsync(game, versionId, cancellationToken);
        _cache[key] = resolved;
        return resolved;
    }

    private async Task<MinecraftLaunchCapabilities> InspectAsync(Game game, string versionId, CancellationToken cancellationToken)
    {
        try
        {
            var supportsQuickPlay = await InspectVersionChainAsync(game.Path.Versions, versionId, new(StringComparer.OrdinalIgnoreCase), cancellationToken);
            return supportsQuickPlay
                ? new(true, true)
                : new(true, false, "This Minecraft version does not expose singleplayer Quick Play.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Could not inspect launch capabilities for version {VersionId}.", versionId);
            return new(true, false, "The custom version metadata could not be read safely.");
        }
    }

    private static async Task<bool> InspectVersionChainAsync(string versionsPath, string versionId, HashSet<string> visited, CancellationToken cancellationToken)
    {
        if (!visited.Add(versionId) || visited.Count > 16) return false;
        var jsonPath = Path.Combine(versionsPath, versionId, versionId + ".json");
        if (!File.Exists(jsonPath)) return false;
        await using var stream = File.OpenRead(jsonPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (ContainsQuickPlaySingleplayer(document.RootElement)) return true;
        return document.RootElement.TryGetProperty("inheritsFrom", out var parent)
            && parent.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(parent.GetString())
            && await InspectVersionChainAsync(versionsPath, parent.GetString()!, visited, cancellationToken);
    }

    private static bool ContainsQuickPlaySingleplayer(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString()?.Contains("quickPlaySingleplayer", StringComparison.Ordinal) == true;
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Any(ContainsQuickPlaySingleplayer);
        if (element.ValueKind == JsonValueKind.Object)
            return element.EnumerateObject().Any(p => ContainsQuickPlaySingleplayer(p.Value));
        return false;
    }
}
