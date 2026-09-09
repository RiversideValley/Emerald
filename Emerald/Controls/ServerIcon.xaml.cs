using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Emerald.Controls;

public sealed partial class ServerIcon : UserControl
{
    private int _loadVersion;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(ServerIcon), new PropertyMetadata(null, OnSourceChanged));

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ServerIcon() => InitializeComponent();

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => _ = ((ServerIcon)sender).LoadAsync(args.NewValue as string);

    private async Task LoadAsync(string? source)
    {
        var version = ++_loadVersion;
        IconImage.Source = null;
        if (string.IsNullOrWhiteSpace(source)) return;

        try
        {
            BitmapImage image;
            if (source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var separator = source.IndexOf(',');
                if (separator < 0 ||
                    !source[..separator].Contains(";base64", StringComparison.OrdinalIgnoreCase)) return;
                var bytes = Convert.FromBase64String(source[(separator + 1)..]);
                if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024) return;

                using var stream = new InMemoryRandomAccessStream();
                image = new BitmapImage();
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync();
                    stream.Seek(0);
                    await image.SetSourceAsync(stream);
                }
            }
            else if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
                image = new BitmapImage(uri);
            }
            else return;

            if (version == _loadVersion) IconImage.Source = image;
        }
        catch (Exception)
        {
            if (version == _loadVersion) IconImage.Source = null;
        }
    }
}
