using System.IO.Compression;
using System.Text.Json;

namespace Emerald.CoreX.Modpacks;

public interface IMrPackReader
{
    Task<MrPackManifest> ReadAsync(string mrPackPath, CancellationToken cancellationToken = default);

    Task<MrPackManifest> ReadAsync(Stream mrPackStream, CancellationToken cancellationToken = default);
}

public sealed class MrPackReader : IMrPackReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MrPackManifest> ReadAsync(string mrPackPath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(mrPackPath);
        return await ReadAsync(stream, cancellationToken);
    }

    public async Task<MrPackManifest> ReadAsync(Stream mrPackStream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(mrPackStream, ZipArchiveMode.Read, leaveOpen: true);
        var indexEntry = archive.GetEntry("modrinth.index.json")
            ?? throw new InvalidOperationException("The modpack does not contain modrinth.index.json.");

        await using var indexStream = indexEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<MrPackManifest>(
            indexStream,
            SerializerOptions,
            cancellationToken);

        if (manifest == null)
        {
            throw new InvalidOperationException("The modpack manifest could not be read.");
        }

        Validate(manifest);
        return manifest;
    }

    private static void Validate(MrPackManifest manifest)
    {
        if (manifest.FormatVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported Modrinth pack format version '{manifest.FormatVersion}'.");
        }

        if (!string.Equals(manifest.Game, "minecraft", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Minecraft Modrinth packs are supported.");
        }

        if (string.IsNullOrWhiteSpace(manifest.GetMinecraftVersion()))
        {
            throw new InvalidOperationException("The modpack manifest does not specify a Minecraft dependency.");
        }

        foreach (var file in manifest.Files)
        {
            MrPackPathGuard.GetSafeDestinationPath("/", file.Path);

            if (!file.Hashes.ContainsKey("sha1") || !file.Hashes.ContainsKey("sha512"))
            {
                throw new InvalidOperationException($"The modpack file '{file.Path}' is missing required SHA-1 or SHA-512 hashes.");
            }
        }
    }
}

public static class MrPackPathGuard
{
    public static string GetSafeDestinationPath(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Modpack file paths cannot be empty.");
        }

        var normalizedRelative = relativePath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedRelative)
            || normalizedRelative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or "..")
            || LooksLikeWindowsRoot(relativePath))
        {
            throw new InvalidOperationException($"Unsafe modpack path '{relativePath}'.");
        }

        var root = Path.GetFullPath(rootPath);
        var destination = Path.GetFullPath(Path.Combine(root, normalizedRelative));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;

        if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsafe modpack path '{relativePath}'.");
        }

        return destination;
    }

    private static bool LooksLikeWindowsRoot(string path)
        => path.Length >= 2
           && char.IsLetter(path[0])
           && path[1] == ':';
}
