using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Emerald.CoreX.Installation;

internal static class LocalInstanceManifest
{
    /// <summary>
    /// Reconstructs Emerald's expected-file manifest from version JSON, inherited
    /// version JSON, and the local asset index. This method performs no HTTP calls.
    /// </summary>
    public static async Task<InstanceInstallReceipt?> BuildAsync(Game game, CancellationToken cancellationToken)
    {
        var resolved = game.Version.RealVersion;
        if (string.IsNullOrWhiteSpace(resolved)) return null;

        var files = new Dictionary<string, ExpectedManagedFile>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = resolved;
        // Loader versions commonly inherit from vanilla. Walk that chain while
        // guarding against malformed metadata that contains an inheritance loop.
        while (!string.IsNullOrWhiteSpace(current) && visited.Add(current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var jsonRelative = SafeRelative(Path.Combine(current, current + ".json"));
            var jsonPath = Path.Combine(game.Path.Versions, jsonRelative);
            Add(files, new(ManagedPathRoot.Versions, jsonRelative, FileSize(jsonPath), FileSha1(jsonPath), null,
                ManagedFileCategory.Metadata, IntegritySeverity.Critical));
            if (!File.Exists(jsonPath)) break;

            await using var stream = File.OpenRead(jsonPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("downloads", out var downloads)
                && downloads.TryGetProperty("client", out var client))
            {
                var jarRelative = SafeRelative(Path.Combine(current, current + ".jar"));
                Add(files, FromDownload(ManagedPathRoot.Versions, jarRelative, client,
                    ManagedFileCategory.Client, IntegritySeverity.Critical));
            }

            if (root.TryGetProperty("libraries", out var libraries))
            {
                foreach (var library in libraries.EnumerateArray())
                {
                    if (!IsAllowedOnCurrentPlatform(library) || !library.TryGetProperty("downloads", out var libraryDownloads)) continue;
                    if (libraryDownloads.TryGetProperty("artifact", out var artifact)
                        && artifact.TryGetProperty("path", out var artifactPath))
                    {
                        Add(files, FromDownload(ManagedPathRoot.Libraries, SafeRelative(artifactPath.GetString()!), artifact,
                            ManagedFileCategory.Library, IntegritySeverity.Critical));
                    }

                    if (libraryDownloads.TryGetProperty("classifiers", out var classifiers))
                    {
                        foreach (var classifier in classifiers.EnumerateObject())
                        {
                            if (!IsCurrentNativeClassifier(classifier.Name) || !classifier.Value.TryGetProperty("path", out var nativePath)) continue;
                            Add(files, FromDownload(ManagedPathRoot.Libraries, SafeRelative(nativePath.GetString()!), classifier.Value,
                                ManagedFileCategory.Native, IntegritySeverity.Critical));
                        }
                    }
                }
            }

            if (root.TryGetProperty("assetIndex", out var assetIndex)
                && assetIndex.TryGetProperty("id", out var assetId))
            {
                var indexRelative = SafeRelative(Path.Combine("indexes", assetId.GetString()! + ".json"));
                var indexFile = FromDownload(ManagedPathRoot.Assets, indexRelative, assetIndex,
                    ManagedFileCategory.Metadata, IntegritySeverity.Critical);
                Add(files, indexFile);
                AddAssetObjects(game, files, indexRelative, cancellationToken);
            }

            if (root.TryGetProperty("logging", out var logging)
                && logging.TryGetProperty("client", out var loggingClient)
                && loggingClient.TryGetProperty("file", out var loggingFile)
                && loggingFile.TryGetProperty("id", out var loggingId))
            {
                Add(files, FromDownload(ManagedPathRoot.Assets,
                    SafeRelative(Path.Combine("log_configs", loggingId.GetString()!)), loggingFile,
                    ManagedFileCategory.Logging, IntegritySeverity.Critical));
            }

            current = root.TryGetProperty("inheritsFrom", out var inherited) ? inherited.GetString() : null;
        }

        var list = files.Values.OrderBy(x => x.Root).ThenBy(x => x.RelativePath, StringComparer.Ordinal).ToList();
        return new InstanceInstallReceipt
        {
            ResolvedVersion = resolved,
            Loader = game.Version.Type.ToString(),
            PathLayoutFingerprint = ComputePathFingerprint(game),
            ManifestFingerprint = ComputeManifestFingerprint(list),
            Files = list
        };
    }

    public static string Resolve(Game game, ExpectedManagedFile file)
    {
        var root = file.Root switch
        {
        ManagedPathRoot.Instance => game.Path.BasePath,
        ManagedPathRoot.Assets => game.Path.Assets,
        ManagedPathRoot.Libraries => game.Path.Library,
        ManagedPathRoot.Runtime => game.Path.Runtime,
        ManagedPathRoot.Versions => game.Path.Versions,
        _ => game.Path.BasePath
        };
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relative = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        // Receipts are persistent input. Never permit an absolute or ../ path to
        // make verification read outside the declared managed root.
        if (Path.IsPathRooted(relative)) return Path.Combine(fullRoot, ".emerald-invalid-managed-path");
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relative));
        var prefix = fullRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return Path.Combine(fullRoot, ".emerald-invalid-managed-path");
        return candidate;
    }

    public static string ComputePathFingerprint(Game game)
    {
        // Detect changes to split/shared assets, libraries, runtime, or versions
        // even when all receipt-relative paths still look valid.
        var value = string.Join('\n', game.Path.BasePath, game.Path.Assets, game.Path.Library, game.Path.Runtime, game.Path.Versions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static void AddAssetObjects(Game game, Dictionary<string, ExpectedManagedFile> files, string indexRelative, CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(game.Path.Assets, indexRelative);
        if (!File.Exists(indexPath)) return;
        using var document = JsonDocument.Parse(File.ReadAllBytes(indexPath));
        if (!document.RootElement.TryGetProperty("objects", out var objects)) return;
        foreach (var item in objects.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!item.Value.TryGetProperty("hash", out var hashElement)) continue;
            var hash = hashElement.GetString();
            if (string.IsNullOrWhiteSpace(hash) || hash.Length < 2) continue;
            var relative = SafeRelative(Path.Combine("objects", hash[..2], hash));
            Add(files, new(ManagedPathRoot.Assets, relative, GetLong(item.Value, "size"), hash, null,
                ManagedFileCategory.Asset, IntegritySeverity.Critical,
                $"https://resources.download.minecraft.net/{hash[..2]}/{hash}"));
        }
    }

    private static ExpectedManagedFile FromDownload(ManagedPathRoot root, string relative, JsonElement element,
        ManagedFileCategory category, IntegritySeverity severity) => new(root, relative, GetLong(element, "size"),
        GetString(element, "sha1"), null, category, severity, GetString(element, "url"));

    private static bool IsAllowedOnCurrentPlatform(JsonElement library)
    {
        if (!library.TryGetProperty("rules", out var rules)) return true;
        var allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            var matches = !rule.TryGetProperty("os", out var os)
                || !os.TryGetProperty("name", out var name)
                || string.Equals(name.GetString(), CurrentOsName(), StringComparison.OrdinalIgnoreCase);
            if (matches && rule.TryGetProperty("action", out var action)) allowed = action.GetString() == "allow";
        }
        return allowed;
    }

    private static bool IsCurrentNativeClassifier(string classifier)
    {
        var os = CurrentOsName();
        return classifier.Contains("natives-" + os, StringComparison.OrdinalIgnoreCase)
            || (os == "osx" && classifier.Contains("natives-macos", StringComparison.OrdinalIgnoreCase));
    }

    private static string CurrentOsName() => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "osx" : "linux";
    private static string? GetString(JsonElement element, string property) => element.TryGetProperty(property, out var value) ? value.GetString() : null;
    private static long? GetLong(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var number) ? number : null;
    private static long? FileSize(string path) => File.Exists(path) ? new FileInfo(path).Length : null;
    private static string? FileSha1(string path) => File.Exists(path) ? Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(path))).ToLowerInvariant() : null;
    private static string SafeRelative(string path) => path.Replace('\\', '/').TrimStart('/');
    private static void Add(Dictionary<string, ExpectedManagedFile> files, ExpectedManagedFile file) => files[$"{file.Root}:{file.RelativePath}"] = file;
    private static string ComputeManifestFingerprint(IEnumerable<ExpectedManagedFile> files)
    {
        var text = string.Join('\n', files.Select(x => $"{x.Root}|{x.RelativePath}|{x.Size}|{x.Sha1}|{x.Sha512}|{x.Severity}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
