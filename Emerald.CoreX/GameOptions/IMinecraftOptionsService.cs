using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Emerald.CoreX.GameOptions;

public interface IMinecraftOptionsService
{
    /// <summary>
    /// Parses options.txt and loads the lang file from the version JAR.
    /// Returns an empty list if options.txt does not exist yet.
    /// </summary>
    Task<MinecraftOptionsLoadResult> LoadAsync(
        Game game,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes entries back to options.txt, preserving any keys that were
    /// not parsed into MinecraftOptionEntry objects.
    /// </summary>
    Task SaveAsync(
        Game game,
        IEnumerable<MinecraftOptionEntry> entries,
        CancellationToken cancellationToken = default);
}

public sealed class MinecraftOptionsLoadResult
{
    public IReadOnlyList<MinecraftOptionEntry> Entries { get; init; } = [];
    public bool OptionsFileExists { get; init; }
    /// <summary>Full original key→value map, used to round-trip unknown keys on save.</summary>
    public IReadOnlyDictionary<string, string> OriginalRaw { get; init; }
        = new Dictionary<string, string>();
}
