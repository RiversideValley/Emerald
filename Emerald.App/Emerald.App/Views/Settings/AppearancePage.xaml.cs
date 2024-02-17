using Emerald.Core;
using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using Windows.UI;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

namespace Emerald.WinUI.Views.Settings;

public sealed partial class AppearancePage : Page
{
    public ObservableCollection<Color> TintColorsList { get; } = new()
    {
        Color.FromArgb(255, 255, 185, 0),
        Color.FromArgb(255, 255, 140, 0),
        Color.FromArgb(255, 247, 99, 12),
        Color.FromArgb(255, 202, 80, 16),
        Color.FromArgb(255, 218, 59, 1),
        Color.FromArgb(255, 239, 105, 80),
        Color.FromArgb(255, 209, 52, 56),
        Color.FromArgb(255, 255, 67, 67),
        Color.FromArgb(255, 231, 72, 86),
        Color.FromArgb(255, 232, 17, 35),
        Color.FromArgb(255, 234, 0, 94),
        Color.FromArgb(255, 195, 0, 82),
        Color.FromArgb(255, 227, 0, 140),
        Color.FromArgb(255, 191, 0, 119),
        Color.FromArgb(255, 194, 57, 179),
        Color.FromArgb(255, 154, 0, 137),
        Color.FromArgb(255, 0, 120, 212),
        Color.FromArgb(255, 0, 99, 177),
        Color.FromArgb(255, 142, 140, 216),
        Color.FromArgb(255, 107, 105, 214),
        Color.FromArgb(255, 135, 100, 184),
        Color.FromArgb(255, 116, 77, 169),
        Color.FromArgb(255, 177, 70, 194),
        Color.FromArgb(255, 136, 23, 152),
        Color.FromArgb(255, 0, 153, 188),
        Color.FromArgb(255, 45, 125, 154),
        Color.FromArgb(255, 0, 183, 195),
        Color.FromArgb(255, 3, 131, 135),
        Color.FromArgb(255, 0, 178, 148),
        Color.FromArgb(255, 1, 133, 116),
        Color.FromArgb(255, 0, 204, 106),
        Color.FromArgb(255, 16, 137, 62),
        Color.FromArgb(255, 122, 117, 116),
        Color.FromArgb(255, 93, 90, 88),
        Color.FromArgb(255, 104, 118, 138),
        Color.FromArgb(255, 81, 92, 107),
        Color.FromArgb(255, 86, 124, 115),
        Color.FromArgb(255, 72, 104, 96),
        Color.FromArgb(255, 73, 130, 5),
        Color.FromArgb(255, 16, 124, 16),
        Color.FromArgb(255, 118, 118, 118),
        Color.FromArgb(255, 76, 74, 72),
        Color.FromArgb(255, 105, 121, 126),
        Color.FromArgb(255, 74, 84, 89),
        Color.FromArgb(255, 100, 124, 100),
        Color.FromArgb(255, 82, 94, 84),
        Color.FromArgb(255, 132, 117, 69),
        Color.FromArgb(255, 126, 115, 95)
    };

    public AppearancePage()
    {
        InitializeComponent();

        if (SS.Settings.App.Appearance.MicaTintColor == (int)Helpers.Settings.Enums.MicaTintColor.CustomColor)
        {
            if (SS.Settings.App.Appearance.CustomMicaTintColor != null)
            {
                var c = SS.Settings.App.Appearance.CustomMicaTintColor;
                var cl = Color.FromArgb((byte)c.Value.A, (byte)c.Value.R, (byte)c.Value.G, (byte)c.Value.B);

                if (TintColorsList.Contains(cl))
                    GVColorList.SelectedIndex = TintColorsList.IndexOf(cl);
                else
                {
                    TintColorsList.Add(cl);
                    GVColorList.SelectedIndex = TintColorsList.Count - 1;
                }
            }
        }
    }

    private void GVColorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var c = TintColorsList[GVColorList.SelectedIndex];

        SS.Settings.App.Appearance.MicaTintColor = (int)Helpers.Settings.Enums.MicaTintColor.CustomColor;
        SS.Settings.App.Appearance.CustomMicaTintColor = (c.A, c.R, c.G, c.B);
    }

    private void CustomTintColor_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {

        var c = SS.Settings.App.Appearance.CustomMicaTintColor;
        var cp = new ColorPicker()
        {
            ColorSpectrumShape = ColorSpectrumShape.Box,
            IsMoreButtonVisible = false,
            IsColorSliderVisible = true,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
            IsAlphaEnabled = true,
            IsAlphaSliderVisible = true,
            IsAlphaTextInputVisible = false,
            Color = Color.FromArgb((byte)(c?.A ?? 0), (byte)(c?.R ?? 0), (byte)(c?.G ?? 0), (byte)(c?.B ?? 0))
        };

        var d = cp.ToContentDialog("CreateAColor".Localize(), Localized.Cancel.Localize(), ContentDialogButton.Primary);

        d.PrimaryButtonText = Localized.OK.Localize();

        d.PrimaryButtonClick += (_, _) =>
        {
            var cl = cp.Color;
            if (TintColorsList.Contains(cl))
            {
                GVColorList.SelectedIndex = TintColorsList.IndexOf(cl);
            }
            else
            {
                TintColorsList.Add(cl);
                GVColorList.SelectedIndex = TintColorsList.Count - 1;
            }
        };

        _ = d.ShowAsync();
    }
}
