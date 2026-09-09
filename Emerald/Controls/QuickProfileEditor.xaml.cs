using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Services;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.UI;

namespace Emerald.Controls;

public sealed partial class QuickProfileEditor : UserControl
{
    private const int BlockPageSize = 36;
    private IReadOnlyList<BlockIconOption> _allBlocks = [];
    private int _blockPage;
    private bool _settingColor;
    private bool _loadedBlocks;

    public QuickProfileEditorViewModel ViewModel { get; }
    public ObservableCollection<QuickProfileGlyphOption> GlyphItems { get; } = [];
    public ObservableCollection<BlockIconOption> BlockItems { get; } = [];
    public ObservableCollection<ProfileColorOption> ColorItems { get; } = [];

    public QuickProfileEditor(QuickProfileEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        Setup.IsExpanded = ViewModel.HasError;
        BuildColorItems();
        SetPickerColor(ViewModel.Accent?.Value ?? 0xFF107C10);
        UpdateIconType();
        UpdateGlyphItems();
    }

    private async void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedBlocks) return;
        _loadedBlocks = true;
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/blocks/blocks.json"));
            await using var stream = await file.OpenStreamForReadAsync();
            _allBlocks = await JsonSerializer.DeserializeAsync<List<BlockIconOption>>(stream) ?? [];
            if (ViewModel.IsBlockIcon && ViewModel.BlockIconFileName is { } selected)
            {
                var index = _allBlocks.ToList().FindIndex(x => x.FileName == selected);
                if (index >= 0) _blockPage = index / BlockPageSize;
            }
        }
        catch
        {
            _allBlocks = [];
        }

        UpdateBlockItems();
        SelectCurrentItems();
    }

    private void BuildColorItems()
    {
        ColorItems.Clear();
        foreach (var color in ViewModel.Colors)
            ColorItems.Add(new ProfileColorOption(color.Value, color.Label));
        SelectCurrentItems();
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
        SelectCurrentItems();
    }

    private void IconSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _blockPage = 0;
        UpdateGlyphItems();
        UpdateBlockItems();
        SelectCurrentItems();
    }

    private void UpdateGlyphItems()
    {
        GlyphItems.Clear();
        var query = IconSearch?.Text?.Trim() ?? string.Empty;
        foreach (var icon in ViewModel.Icons.Where(x => Matches(x.Key, query) || Matches(x.Label, query)))
            GlyphItems.Add(icon);
    }

    private void UpdateBlockItems()
    {
        BlockItems.Clear();
        var matches = FilteredBlocks().ToArray();
        var pageCount = Math.Max(1, (int)Math.Ceiling(matches.Length / (double)BlockPageSize));
        _blockPage = Math.Clamp(_blockPage, 0, pageCount - 1);
        foreach (var block in matches.Skip(_blockPage * BlockPageSize).Take(BlockPageSize))
            BlockItems.Add(block);

        BlockPageText.Text = matches.Length == 0 ? DashboardText.Get("NoMatches") : DashboardText.Format("PageOf", _blockPage + 1, pageCount);
        PreviousBlockButton.IsEnabled = _blockPage > 0;
        NextBlockButton.IsEnabled = _blockPage + 1 < pageCount;
    }

    private IEnumerable<BlockIconOption> FilteredBlocks()
    {
        var query = IconSearch?.Text?.Trim() ?? string.Empty;
        return _allBlocks.Where(x => Matches(x.Name, query) || Matches(x.FileName, query));
    }

    private void GlyphChoices_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is QuickProfileGlyphOption icon)
        {
            ViewModel.SelectGlyph(icon);
            SelectCurrentItems();
        }
    }

    private void BlockChoices_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BlockIconOption block)
        {
            ViewModel.SelectBlock(block.FileName);
            SelectCurrentItems();
        }
    }

    private void ColorChoices_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ProfileColorOption color)
        {
            ViewModel.SetAccent(color.Value);
            SetPickerColor(color.Value);
            SelectCurrentItems();
        }
    }

    private void SelectCurrentItems()
    {
        if (GlyphChoices == null) return;
        GlyphChoices.SelectedItem = GlyphItems.FirstOrDefault(x => ViewModel.IsGlyphIcon && x.Key == ViewModel.Icon?.Key);
        BlockChoices.SelectedItem = BlockItems.FirstOrDefault(x => ViewModel.IsBlockIcon && x.FileName == ViewModel.BlockIconFileName);
        ColorChoices.SelectedItem = ColorItems.FirstOrDefault(x => x.Value == ViewModel.Accent?.Value);
    }

    private void PreviousBlock_Click(object sender, RoutedEventArgs e)
    {
        if (_blockPage <= 0) return;
        _blockPage--;
        UpdateBlockItems();
        SelectCurrentItems();
    }

    private void NextBlock_Click(object sender, RoutedEventArgs e)
    {
        _blockPage++;
        UpdateBlockItems();
        SelectCurrentItems();
    }

    private void CustomColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_settingColor) return;
        var color = args.NewColor;
        var argb = 0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        ViewModel.SetAccent(argb);
        CustomColorSwatch.Background = Brush(argb);
        SelectCurrentItems();
    }

    private void SetPickerColor(uint argb)
    {
        _settingColor = true;
        CustomColorPicker.Color = Color.FromArgb(255, (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        CustomColorSwatch.Background = Brush(argb);
        _settingColor = false;
    }

    private static bool Matches(string value, string query) => string.IsNullOrWhiteSpace(query) || value.Contains(query, StringComparison.OrdinalIgnoreCase);
    private static SolidColorBrush Brush(uint argb) => new(Color.FromArgb(255, (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
}

public sealed class ProfileColorOption
{
    public uint Value { get; }
    public string Label { get; }
    public SolidColorBrush Brush { get; }

    public ProfileColorOption(uint value, string label)
    {
        Value = value;
        Label = label;
        Brush = new(Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value));
    }
}

public sealed class BlockIconOption
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonIgnore]
    public BitmapImage Image => new(new Uri($"ms-appx:///Assets/blocks/{Uri.EscapeDataString(Path.GetFileName(FileName))}"));
}
