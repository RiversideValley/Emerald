using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Emerald.Controls;

public sealed partial class WorldThumbnail : UserControl
{
    private static readonly Dictionary<string, (DateTime Modified, ImageSource Image)> Cache = new();
    private int _version;

    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(nameof(Path), typeof(string),
        typeof(WorldThumbnail), new PropertyMetadata(null, Changed));

    public string? Path
    {
        get => (string?)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public WorldThumbnail()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = LoadAsync();
    }

    public static void InvalidateCache()
    {
        Cache.Clear();
    }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        _ = ((WorldThumbnail)sender).LoadAsync();
    }

    private async Task LoadAsync()
    {
        var version = ++_version;
        Picture.Source = null;
        var path = Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 1024 * 1024)
            {
                return;
            }

            if (Cache.TryGetValue(file.FullName, out var cached) && cached.Modified == file.LastWriteTimeUtc)
            {
                Picture.Source = cached.Image;
                return;
            }

            var bytes = await File.ReadAllBytesAsync(file.FullName);
            using var data = SkiaSharp.SKData.CreateCopy(bytes);
            using var codec = SkiaSharp.SKCodec.Create(data);
            if (codec == null || codec.Info.Width > 1024 || codec.Info.Height > 1024)
            {
                return;
            }

            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            if (version != _version)
            {
                return;
            }

            if (Cache.Count > 128)
            {
                Cache.Clear();
            }

            Cache[file.FullName] = (file.LastWriteTimeUtc, bitmap);
            Picture.Source = bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or System.Runtime.InteropServices.COMException)
        {
        }
    }
}
