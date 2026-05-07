using System.Text.Json.Serialization;
using GameVersionType = Emerald.CoreX.Versions.Type;

namespace Emerald.CoreX.Modpacks;

public sealed class MrPackManifest
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("versionId")]
    public string VersionId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("files")]
    public List<MrPackFile> Files { get; set; } = [];

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string> Dependencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetMinecraftVersion()
        => Dependencies.TryGetValue("minecraft", out var version)
            ? version
            : null;

    public MrPackLoaderDependency GetLoaderDependency()
    {
        if (Dependencies.TryGetValue("fabric-loader", out var fabric))
        {
            return new MrPackLoaderDependency(GameVersionType.Fabric, fabric, "Fabric");
        }

        if (Dependencies.TryGetValue("quilt-loader", out var quilt))
        {
            return new MrPackLoaderDependency(GameVersionType.Quilt, quilt, "Quilt");
        }

        if (Dependencies.TryGetValue("neoforge", out var neoForge))
        {
            return new MrPackLoaderDependency(GameVersionType.NeoForge, neoForge, "NeoForge");
        }

        if (Dependencies.TryGetValue("forge", out var forge))
        {
            return new MrPackLoaderDependency(GameVersionType.Forge, forge, "Forge");
        }

        return new MrPackLoaderDependency(GameVersionType.Vanilla, null, "Vanilla");
    }
}

public sealed class MrPackLoaderDependency(GameVersionType type, string? version, string displayName)
{
    public GameVersionType Type { get; } = type;

    public string? Version { get; } = version;

    public string DisplayName { get; } = displayName;
}

public sealed class MrPackFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("env")]
    public MrPackFileEnvironment? Environment { get; set; }

    [JsonPropertyName("downloads")]
    public List<string> Downloads { get; set; } = [];

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    public bool IsClientEligible
    {
        get
        {
            var client = Environment?.Client;
            return string.IsNullOrWhiteSpace(client)
                   || client.Equals("required", StringComparison.OrdinalIgnoreCase)
                   || client.Equals("optional", StringComparison.OrdinalIgnoreCase);
        }
    }
}

public sealed class MrPackFileEnvironment
{
    [JsonPropertyName("client")]
    public string? Client { get; set; }

    [JsonPropertyName("server")]
    public string? Server { get; set; }
}
