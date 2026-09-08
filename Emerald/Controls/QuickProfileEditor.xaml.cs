using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Services;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.UI;

namespace Emerald.Controls;

public sealed partial class QuickProfileEditor : UserControl
{
    private const int BlockPageSize = 12;
    private readonly List<ToggleButton> _colorButtons = [];
    private IReadOnlyList<BlockIconOption> _blocks = [];
    private int _blockPage;
    private bool _settingColor;
    private bool _loadedBlocks;

    public QuickProfileEditorViewModel ViewModel { get; }

    public QuickProfileEditor(QuickProfileEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        Setup.IsExpanded = ViewModel.HasError;
        BuildColorChoices();
        SetPickerColor(ViewModel.Accent?.Value ?? 0xFF107C10);
        UpdateIconType();
        RenderGlyphs();
    }

    private async void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedBlocks) return;
        _loadedBlocks = true;
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/blocks/blocks.json"));
            await using var stream = await file.OpenStreamForReadAsync();
            _blocks = await JsonSerializer.DeserializeAsync<List<BlockIconOption>>(stream) ?? [];
            if (ViewModel.IsBlockIcon && ViewModel.BlockIconFileName is { } selected)
            {
                var index = FilteredBlocks().ToList().FindIndex(x => x.FileName == selected);
                if (index >= 0) _blockPage = index / BlockPageSize;
            }
        }
        catch
        {
            _blocks = [];
        }

        RenderBlocks();
    }

    private void BuildColorChoices()
    {
        foreach (var color in ViewModel.Colors)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new Border { Width = 18, Height = 18, CornerRadius = new(9), Background = Brush(color.Value) });
            row.Children.Add(new TextBlock { Text = color.Label });
            var button = new ToggleButton { Content = row, IsChecked = ViewModel.Accent?.Value == color.Value };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, color.Label);
            button.Click += (_, _) =>
            {
                ViewModel.SetAccent(color.Value);
                SetPickerColor(color.Value);
                UpdateColorSelection();
            };
            Grid.SetColumn(button, ColorChoices.Children.Count % 3);
            Grid.SetRow(button, ColorChoices.Children.Count / 3);
            ColorChoices.Children.Add(button);
            _colorButtons.Add(button);
        }
    }

    private void GlyphType_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Icon is { } icon) ViewModel.SelectGlyph(icon);
        UpdateIconType();
    }

    private void BlockType_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IconKind = QuickProfileIconKind.Block;
        UpdateIconType();
    }

    private void UpdateIconType()
    {
        GlyphTypeButton.IsChecked = ViewModel.IsGlyphIcon;
        BlockTypeButton.IsChecked = ViewModel.IsBlockIcon;
        GlyphChoices.Visibility = ViewModel.IsGlyphIcon ? Visibility.Visible : Visibility.Collapsed;
        BlockPicker.Visibility = ViewModel.IsBlockIcon ? Visibility.Visible : Visibility.Collapsed;
        if (ViewModel.IsGlyphIcon) RenderGlyphs(); else RenderBlocks();
    }

    private void IconSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _blockPage = 0;
        RenderGlyphs();
        RenderBlocks();
    }

    private void RenderGlyphs()
    {
        if (GlyphChoices == null) return;
        PrepareGrid(GlyphChoices, 4);
        var query = IconSearch?.Text?.Trim() ?? string.Empty;
        var icons = ViewModel.Icons.Where(x => Matches(x.Key, query) || Matches(x.Label, query)).ToArray();
        for (var index = 0; index < icons.Length; index++)
        {
            var icon = icons[index];
            var content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new FontIcon { Glyph = icon.Glyph, FontSize = 22 });
            content.Children.Add(new TextBlock { Text = icon.Label, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });
            var button = new ToggleButton { Content = content, Height = 64, HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = ViewModel.IsGlyphIcon && ViewModel.Icon?.Key == icon.Key };
            ToolTipService.SetToolTip(button, icon.Label);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, icon.Label);
            button.Click += (_, _) => { ViewModel.SelectGlyph(icon); UpdateIconType(); };
            Grid.SetColumn(button, index % 4);
            Grid.SetRow(button, index / 4);
            GlyphChoices.Children.Add(button);
        }
    }

    private void RenderBlocks()
    {
        if (BlockChoices == null || BlockPageText == null) return;
        PrepareGrid(BlockChoices, 4);
        var matches = FilteredBlocks().ToArray();
        var pageCount = Math.Max(1, (int)Math.Ceiling(matches.Length / (double)BlockPageSize));
        _blockPage = Math.Clamp(_blockPage, 0, pageCount - 1);
        var page = matches.Skip(_blockPage * BlockPageSize).Take(BlockPageSize).ToArray();
        for (var index = 0; index < page.Length; index++)
        {
            var block = page[index];
            var content = new Grid { ColumnSpacing = 8 };
            content.ColumnDefinitions.Add(new() { Width = new GridLength(40) });
            content.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            content.Children.Add(new Image { Source = new BitmapImage(BlockUri(block.FileName)), Width = 40, Height = 40, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var label = new TextBlock { Text = block.Name, MaxLines = 2, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 1);
            content.Children.Add(label);
            var button = new ToggleButton { Content = content, Height = 64, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsChecked = ViewModel.IsBlockIcon && ViewModel.BlockIconFileName == block.FileName };
            ToolTipService.SetToolTip(button, block.Name);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, block.Name);
            button.Click += (_, _) => { ViewModel.SelectBlock(block.FileName); UpdateIconType(); };
            Grid.SetColumn(button, index % 4);
            Grid.SetRow(button, index / 4);
            BlockChoices.Children.Add(button);
        }

        BlockPageText.Text = matches.Length == 0 ? DashboardText.Get("NoMatches") : DashboardText.Format("PageOf", _blockPage + 1, pageCount);
        PreviousBlockButton.IsEnabled = _blockPage > 0;
        NextBlockButton.IsEnabled = _blockPage + 1 < pageCount;
    }

    private IEnumerable<BlockIconOption> FilteredBlocks()
    {
        var query = IconSearch?.Text?.Trim() ?? string.Empty;
        return _blocks.Where(x => Matches(x.Name, query) || Matches(x.FileName, query));
    }

    private void PreviousBlock_Click(object sender, RoutedEventArgs e) { _blockPage--; RenderBlocks(); }
    private void NextBlock_Click(object sender, RoutedEventArgs e) { _blockPage++; RenderBlocks(); }

    private void CustomColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_settingColor) return;
        var color = args.NewColor;
        var argb = 0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        ViewModel.SetAccent(argb);
        CustomColorSwatch.Background = Brush(argb);
        UpdateColorSelection();
    }

    private void SetPickerColor(uint argb)
    {
        _settingColor = true;
        CustomColorPicker.Color = Color.FromArgb(255, (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        CustomColorSwatch.Background = Brush(argb);
        _settingColor = false;
    }

    private void UpdateColorSelection()
    {
        for (var index = 0; index < _colorButtons.Count; index++)
            _colorButtons[index].IsChecked = ViewModel.Accent?.Value == ViewModel.Colors[index].Value;
    }

    private static void PrepareGrid(Grid grid, int columns)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var column = 0; column < columns; column++) grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 3; row++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
    }

    private static bool Matches(string value, string query) => string.IsNullOrWhiteSpace(query) || value.Contains(query, StringComparison.OrdinalIgnoreCase);
    private static Uri BlockUri(string fileName) => new($"ms-appx:///Assets/blocks/{Uri.EscapeDataString(Path.GetFileName(fileName))}");
    private static SolidColorBrush Brush(uint argb) => new(Color.FromArgb(255, (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

    private sealed record BlockIconOption(
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("name")] string Name);
}
