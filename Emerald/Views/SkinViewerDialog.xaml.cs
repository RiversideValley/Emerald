using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emerald.Controls;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage.Pickers;

namespace Emerald.Views;

public sealed partial class SkinViewerDialog : ContentDialog
{
    private const double NarrowLayoutThreshold = 820;

    private readonly IAccountService? _accountService;
    private readonly EAccount? _account;
    private readonly CancellationTokenSource _cancellationSource = new();
    private bool _isSettingsOpen;

    public SkinViewerDialog()
    {
        InitializeComponent();
        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        this.StretchToWindow();

        PrimaryButtonText = "SkinViewerShowSettings".Localize();
        CloseButtonText = "Close".Localize();
        DefaultButton = ContentDialogButton.Close;

        PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            ToggleSettings();
        };
        RootGrid.SizeChanged += RootGrid_SizeChanged;
        Closing += OnClosing;
    }

    public SkinViewerDialog(IAccountService accountService, EAccount account, XamlRoot xamlRoot) : this()
    {
        _accountService = accountService;
        _account = account;
        XamlRoot = xamlRoot;
        Title = string.Format("SkinViewerTitle".Localize(), account.Name);

        SkinWebView.ViewerFailed += OnViewerFailed;
        Opened += OnOpened;
    }

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        if (_accountService is null || _account is null)
            return;

        try
        {
            UpdateResponsiveLayout(RootGrid.ActualWidth, RootGrid.ActualHeight);

            var skin = await _accountService.GetSkinAsync(_account, cancellationToken: _cancellationSource.Token);
            FallbackPreview.Source = await MinecraftSkinImageFactory.CreateBodyPreviewAsync(skin);
            await SkinWebView.SetSkinAsync(skin, cancellationToken: _cancellationSource.Token);
            _cancellationSource.Token.ThrowIfCancellationRequested();
            SettingsPanel.Opacity = 1;
            StatusTextBlock.Text = skin.IsFallback ? "SkinFallback".Localize() : "SkinDragHint".Localize();
            StatusTextBlock.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) when (_cancellationSource.IsCancellationRequested)
        {
            // Closing the dialog is an expected cancellation path.
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "SkinViewerDialog Unhandled Exception");
            ShowStaticPreview();
        }
        finally
        {
            if (!_cancellationSource.IsCancellationRequested)
                LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private void ToggleSettings()
    {
        _isSettingsOpen = !_isSettingsOpen;
        UpdateResponsiveLayout(RootGrid.ActualWidth, RootGrid.ActualHeight);
        ApplyViewerSettings();
    }

    private void UpdateResponsiveLayout(double availableWidth, double availableHeight)
    {
        if (availableWidth <= 0)
            return;

        var isNarrow = availableWidth < NarrowLayoutThreshold;

        if (isNarrow)
        {
            // Stacked layout: 3D viewer on top, settings below it
            ViewerColumn.Width = new GridLength(1, GridUnitType.Star);
            SettingsColumn.Width = new GridLength(0);
            ViewerRow.Height = GridLength.Auto;
            SettingsRow.Height = GridLength.Auto;

            Grid.SetColumn(ViewerContainer, 0);
            Grid.SetRow(ViewerContainer, 0);

            Grid.SetColumn(SettingsContainer, 0);
            Grid.SetRow(SettingsContainer, 1);

            SettingsContainer.Margin = new Thickness(0, 16, 0, 0);
            ViewerContainer.Height = Math.Max(320, Math.Min(460, availableHeight * 0.45));
            ViewerContainer.VerticalAlignment = VerticalAlignment.Top;
        }
        else
        {
            // Side-by-side layout: 3D viewer on left, settings on right
            ViewerColumn.Width = new GridLength(1, GridUnitType.Star);
            SettingsColumn.Width = _isSettingsOpen ? new GridLength(460) : new GridLength(0);
            ViewerRow.Height = new GridLength(1, GridUnitType.Star);
            SettingsRow.Height = new GridLength(0);

            Grid.SetColumn(ViewerContainer, 0);
            Grid.SetRow(ViewerContainer, 0);

            Grid.SetColumn(SettingsContainer, 1);
            Grid.SetRow(SettingsContainer, 0);

            SettingsContainer.Margin = new Thickness(16, 0, 0, 0);
            ViewerContainer.Height = double.NaN;
            ViewerContainer.VerticalAlignment = VerticalAlignment.Stretch;
        }

        SettingsContainer.Visibility = _isSettingsOpen ? Visibility.Visible : Visibility.Collapsed;
        PrimaryButtonText = (_isSettingsOpen ? "SkinViewerHideSettings" : "SkinViewerShowSettings").Localize();
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyViewerSettings();
    private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => ApplyViewerSettings();
    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e) => ApplyViewerSettings();
    private void CheckBox_CheckChanged(object sender, RoutedEventArgs e) => ApplyViewerSettings();
    private void CapeUrlBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyViewerSettings();

    private async void BrowseCape_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                CommitButtonText = "Select".Localize()
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");

#if WINDOWS
            if (App.Current.MainWindow != null)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(
                    picker,
                    WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));
            }
#endif

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var bytes = await File.ReadAllBytesAsync(file.Path);
                CapeUrlBox.Text = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
                if (BackEquipmentComboBox.SelectedIndex == 0)
                {
                    BackEquipmentComboBox.SelectedIndex = 1; // Switch to Cape
                }
            }
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to pick cape file");
        }
    }

    private void ClearCape_Click(object sender, RoutedEventArgs e)
    {
        CapeUrlBox.Text = string.Empty;
        BackEquipmentComboBox.SelectedIndex = 0; // None
    }

    private async void ApplyViewerSettings()
    {
        if (SettingsContainer.Visibility == Visibility.Collapsed || _cancellationSource.IsCancellationRequested)
            return;

        try
        {
            await SkinWebView.UpdateSettingsAsync(CreateViewerSettings(), _cancellationSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationSource.IsCancellationRequested)
        {
            // Closing the dialog is an expected cancellation path.
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Unable to update skin viewer settings");
        }
    }

    private MinecraftSkinViewerSettings CreateViewerSettings()
    {
        var animation = AnimationComboBox.SelectedItem is ComboBoxItem { Tag: string aniTag }
            && Enum.TryParse<MinecraftSkinViewerAnimation>(aniTag, true, out var parsedAnimation)
                ? parsedAnimation
                : MinecraftSkinViewerAnimation.Idle;

        var equipment = BackEquipmentComboBox.SelectedItem is ComboBoxItem { Tag: string eqTag }
            && Enum.TryParse<MinecraftSkinViewerBackEquipment>(eqTag, true, out var parsedEquipment)
                ? parsedEquipment
                : MinecraftSkinViewerBackEquipment.None;

        var capeUrl = string.IsNullOrWhiteSpace(CapeUrlBox.Text) ? null : CapeUrlBox.Text.Trim();
        if (equipment != MinecraftSkinViewerBackEquipment.None && string.IsNullOrWhiteSpace(capeUrl))
        {
            capeUrl = MinecraftSkinWebView.GetDefaultCapeDataUrl();
        }

        return new MinecraftSkinViewerSettings(
            animation,
            AnimationSpeedSlider.Value,
            RotationEnabledToggle.IsOn,
            RotationSpeedSlider.Value,
            equipment,
            new MinecraftSkinViewerLayers(
                new MinecraftSkinViewerLayer(HeadInnerCheck.IsChecked == true, HeadOuterCheck.IsChecked == true),
                new MinecraftSkinViewerLayer(BodyInnerCheck.IsChecked == true, BodyOuterCheck.IsChecked == true),
                new MinecraftSkinViewerLayer(RightArmInnerCheck.IsChecked == true, RightArmOuterCheck.IsChecked == true),
                new MinecraftSkinViewerLayer(LeftArmInnerCheck.IsChecked == true, LeftArmOuterCheck.IsChecked == true),
                new MinecraftSkinViewerLayer(RightLegInnerCheck.IsChecked == true, RightLegOuterCheck.IsChecked == true),
                new MinecraftSkinViewerLayer(LeftLegInnerCheck.IsChecked == true, LeftLegOuterCheck.IsChecked == true)),
            capeUrl);
    }

    private async void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            _cancellationSource.Cancel();
            SkinWebView.ViewerFailed -= OnViewerFailed;
            await SkinWebView.StopAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnViewerFailed(string message) => ShowStaticPreview();

    private void ShowStaticPreview()
    {
        if (_cancellationSource.IsCancellationRequested)
            return;

        SkinWebView.Visibility = Visibility.Collapsed;
        FallbackPreview.Visibility = Visibility.Visible;
        StatusTextBlock.Text = "Skin3DUnavailable".Localize();
        StatusTextBlock.Visibility = Visibility.Visible;
    }
}
