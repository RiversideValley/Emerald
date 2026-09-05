using Emerald.CoreX.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using Windows.Storage.Streams;

namespace Emerald.Controls;

internal static class MinecraftSkinImageFactory
{
    public static Task<ImageSource> CreateHeadAsync(AccountSkinData skin, int size = 96)
        => CreateAsync(skin, size, size, canvas =>
        {
            using var source = DecodeAndNormalize(skin.PngBytes);
            DrawNearest(canvas, source, new SKRect(8, 8, 16, 16), new SKRect(0, 0, size, size));
            DrawNearest(canvas, source, new SKRect(40, 8, 48, 16), new SKRect(0, 0, size, size));
        });

    public static Task<ImageSource> CreateBodyPreviewAsync(AccountSkinData skin, int width = 180, int height = 260)
        => CreateAsync(skin, width, height, canvas =>
        {
            using var source = DecodeAndNormalize(skin.PngBytes);
            var x = width / 2f;
            DrawNearest(canvas, source, new SKRect(8, 8, 16, 16), new SKRect(x - 36, 12, x + 36, 84));
            DrawNearest(canvas, source, new SKRect(40, 8, 48, 16), new SKRect(x - 36, 12, x + 36, 84));
            DrawNearest(canvas, source, new SKRect(20, 20, 28, 32), new SKRect(x - 36, 84, x + 36, 192));
            DrawNearest(canvas, source, new SKRect(44, 20, 48, 32), new SKRect(x - 72, 88, x - 36, 192));
            DrawNearest(canvas, source, new SKRect(20, 52, 24, 64), new SKRect(x + 36, 88, x + 72, 192));
            DrawNearest(canvas, source, new SKRect(4, 20, 8, 32), new SKRect(x - 36, 192, x, 252));
            DrawNearest(canvas, source, new SKRect(4, 52, 8, 64), new SKRect(x, 192, x + 36, 252));
        });

    private static async Task<ImageSource> CreateAsync(AccountSkinData skin, int width, int height, Action<SKCanvas> draw)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            draw(canvas);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(encoded.ToArray());
            await writer.StoreAsync();
        }

        stream.Seek(0);
        var imageSource = new BitmapImage();
        await imageSource.SetSourceAsync(stream);
        return imageSource;
    }

    internal static SKBitmap DecodeAndNormalize(byte[] pngBytes)
    {
        using var decoded = SKBitmap.Decode(pngBytes)
            ?? throw new InvalidOperationException("The skin texture could not be decoded.");
        if (decoded.Width != 64 || (decoded.Height != 32 && decoded.Height != 64))
            throw new InvalidOperationException("Minecraft skins must be 64×64 or legacy 64×32 PNG textures.");

        var normalized = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(normalized);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(decoded, new SKRect(0, 0, 64, decoded.Height), new SKRect(0, 0, 64, decoded.Height));
        if (decoded.Height == 32)
        {
            // Legacy skins reuse the right-side limb texture. Copying it keeps
            // old accounts recognizable in a modern full-body preview.
            canvas.DrawBitmap(decoded, new SKRect(40, 16, 56, 32), new SKRect(16, 48, 32, 64));
            canvas.DrawBitmap(decoded, new SKRect(0, 16, 16, 32), new SKRect(0, 48, 16, 64));
        }

        return normalized;
    }

    private static void DrawNearest(SKCanvas canvas, SKBitmap source, SKRect sourceRect, SKRect destinationRect)
    {
        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint { IsAntialias = false };
        canvas.DrawImage(
            image,
            sourceRect,
            destinationRect,
            new SKSamplingOptions(SKFilterMode.Nearest),
            paint);
    }
}
