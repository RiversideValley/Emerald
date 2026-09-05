using System.Buffers.Binary;
using System.Linq;
using System.Reflection;

namespace Emerald.CoreX.Models;

/// <summary>The two player-model arm layouts supported by Java Edition skins.</summary>
public enum MinecraftSkinVariant
{
    Classic,
    Slim
}

/// <summary>
/// A validated Minecraft skin texture. This is runtime-only account state and is
/// deliberately not persisted with credentials or account metadata.
/// </summary>
public sealed record AccountSkinData(
    byte[] PngBytes,
    MinecraftSkinVariant Variant,
    string Source,
    bool IsFallback = false);

/// <summary>Small, dependency-free helpers for safe skin texture handling.</summary>
public static class MinecraftSkinTextures
{
    public const int MaxTextureBytes = 1024 * 1024;
    private const string SteveResourceName = "Emerald.SkinViewer.skin_Steve_Default.png";

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly Lazy<byte[]> SteveTexture = new(LoadSteveTexture);

    public static AccountSkinData CreateSteveFallback(string source = "Steve")
        => new(SteveTexture.Value, MinecraftSkinVariant.Classic, source, IsFallback: true);

    public static bool IsSupportedSkinPng(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 24 || pngBytes.Length > MaxTextureBytes)
            return false;

        if (!pngBytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature)
            || !pngBytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            return false;

        var width = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(20, 4));
        return width == 64 && (height == 32 || height == 64);
    }

    private static byte[] LoadSteveTexture()
    {
        var stream = typeof(MinecraftSkinTextures).Assembly.GetManifestResourceStream(SteveResourceName)
            ?? Assembly.GetEntryAssembly()?.GetManifestResourceStream(SteveResourceName);

        if (stream is null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                stream = assembly.GetManifestResourceStream(SteveResourceName);
                if (stream != null) break;
            }
        }

        if (stream is null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                var match = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("skin_Steve_Default.png", StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    stream = assembly.GetManifestResourceStream(match);
                    if (stream != null) break;
                }
            }
        }

        if (stream is null)
        {
            throw new InvalidOperationException($"The embedded Steve skin resource '{SteveResourceName}' could not be found.");
        }

        using (stream)
        using (var memoryStream = new MemoryStream())
        {
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
