using System.Buffers.Binary;
using System.IO.Compression;

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

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly Lazy<byte[]> SteveTexture = new(CreateSteveTexture);

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

    // The default texture is generated once so CoreX remains independent of a UI
    // image library and can always provide an offline Steve-compatible fallback.
    private static byte[] CreateSteveTexture()
    {
        const int size = 64;
        var pixels = new byte[size * size * 4];
        var transparent = new Rgba(0, 0, 0, 0);
        Fill(0, 0, size, size, transparent);

        var skin = new Rgba(214, 165, 111, 255);
        var hair = new Rgba(75, 48, 30, 255);
        var shirt = new Rgba(76, 119, 155, 255);
        var trousers = new Rgba(58, 77, 126, 255);
        var shoes = new Rgba(55, 43, 36, 255);

        // Head atlas (top/under/bottom/right/front/left), then its front hair.
        Fill(0, 0, 32, 16, skin);
        Fill(0, 0, 32, 8, hair);
        Fill(8, 8, 8, 2, hair);
        Fill(8, 10, 1, 5, hair);
        Fill(15, 10, 1, 5, hair);

        // Torso, right arm, and right leg regions in the classic 64x64 atlas.
        Fill(16, 16, 24, 16, shirt);
        Fill(40, 16, 16, 16, shirt);
        Fill(0, 16, 16, 16, trousers);
        Fill(4, 28, 8, 4, shoes);

        // The lower half stores the independent left limb regions on modern skins.
        Fill(16, 48, 16, 16, shirt);
        Fill(0, 48, 16, 16, trousers);
        Fill(4, 60, 8, 4, shoes);

        var raw = new byte[(size * 4 + 1) * size];
        for (var y = 0; y < size; y++)
        {
            var target = y * (size * 4 + 1);
            raw[target] = 0; // PNG filter: none
            Buffer.BlockCopy(pixels, y * size * 4, raw, target + 1, size * 4);
        }

        using var output = new MemoryStream();
        output.Write(PngSignature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, size);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], size);
        header[8] = 8; // RGBA, 8-bit
        header[9] = 6;
        WriteChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw);
        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();

        void Fill(int x, int y, int width, int height, Rgba color)
        {
            for (var row = y; row < y + height; row++)
            for (var column = x; column < x + width; column++)
            {
                var index = (row * size + column) * 4;
                pixels[index] = color.R;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.B;
                pixels[index + 3] = color.A;
            }
        }
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(type, data));
        output.Write(crc);
    }

    private static uint Crc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var crc = 0xffffffffu;
        foreach (var value in first)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }

        foreach (var value in second)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
        }

        return ~crc;
    }

    private readonly record struct Rgba(byte R, byte G, byte B, byte A);
}
