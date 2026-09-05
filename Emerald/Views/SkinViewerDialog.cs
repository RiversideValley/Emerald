using CommunityToolkit.WinUI.Controls;
using Emerald.Controls;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.CoreX.Helpers;
using Emerald.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Emerald.Views;

/// <summary>Dialog orchestration stays in the UI layer; skin retrieval remains in CoreX.</summary>
internal sealed class SkinViewerDialog : ContentDialog
{
    private readonly IAccountService _accountService;
    private readonly EAccount _account;
    private readonly CancellationTokenSource _cancellationSource = new();
    private readonly ProgressRing _progress = new() { IsActive = true, Width = 32, Height = 32 };
    private readonly TextBlock _status = new() { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.WrapWholeWords };
    private readonly Image _fallbackPreview = new() { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
    private readonly MinecraftSkinWebView _viewer = new();
    private readonly Grid _root = new() { Width = 480, Height = 520, RowSpacing = 12 };
    private readonly SplitView _settingsPane = new()
    {
        DisplayMode = SplitViewDisplayMode.Inline,
        PanePlacement = SplitViewPanePlacement.Right,
        OpenPaneLength = 300,
        CompactPaneLength = 0,
        IsPaneOpen = false
    };
    private readonly StackPanel _settingsPanel = new() { Spacing = 12, Visibility = Visibility.Collapsed, Opacity = 0.6 };
    private readonly Button _settingsButton = new();
    private readonly ComboBox _animationBox = new();
    private readonly Slider _animationSpeed = new() { Minimum = 0, Maximum = 3, StepFrequency = 0.1, Value = 1 };
    private readonly ToggleSwitch _rotationEnabled = new() { IsOn = true };
    private readonly Slider _rotationSpeed = new() { Minimum = 0, Maximum = 1, StepFrequency = 0.025, Value = Math.PI / 18d };
    private readonly ComboBox _backEquipmentBox = new();
    private readonly Dictionary<string, (CheckBox Inner, CheckBox Outer)> _layerToggles = new();

    public SkinViewerDialog(IAccountService accountService, EAccount account, XamlRoot xamlRoot)
    {
        _accountService = accountService;
        _account = account;
        XamlRoot = xamlRoot;
        Title = string.Format("SkinViewerTitle".Localize(), account.Name);
        CloseButtonText = "Close".Localize();
        DefaultButton = ContentDialogButton.Close;
        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        Resources["ContentDialogMaxWidth"] = 920d;
        Resources["ContentDialogMaxHeight"] = 660d;

        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var presenter = new Grid
        {
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            CornerRadius = new CornerRadius(8),
            MinHeight = 360
        };
        presenter.Children.Add(_fallbackPreview);
        presenter.Children.Add(_viewer);
        _viewer.ViewerFailed += OnViewerFailed;
        var loading = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12
        };
        loading.Children.Add(_progress);
        loading.Children.Add(new TextBlock { Text = "SkinLoading".Localize(), TextAlignment = TextAlignment.Center });
        presenter.Children.Add(loading);

        _settingsPane.Content = presenter;
        _settingsPane.Pane = CreateSettingsPane();

        _status.Text = "SkinDragHint".Localize();
        _status.Visibility = Visibility.Collapsed;
        _settingsButton.Content = "SkinViewerShowSettings".Localize();
        _settingsButton.Click += (_, _) => ToggleSettingsPane();

        var footer = new Grid { ColumnSpacing = 12 };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(_status);
        Grid.SetColumn(_settingsButton, 1);
        footer.Children.Add(_settingsButton);

        _root.Children.Add(_settingsPane);
        Grid.SetRow(footer, 1);
        _root.Children.Add(footer);
        Content = _root;

        Opened += async (_, _) =>
        {
            try
            {
                var skin = await _accountService.GetSkinAsync(_account, cancellationToken: _cancellationSource.Token);
                _fallbackPreview.Source = await MinecraftSkinImageFactory.CreateBodyPreviewAsync(skin);
                await _viewer.SetSkinAsync(skin, cancellationToken: _cancellationSource.Token);
                _cancellationSource.Token.ThrowIfCancellationRequested();
                _settingsPanel.Visibility = Visibility.Visible;
                _settingsPanel.Opacity = 1;
                _status.Text = skin.IsFallback ? "SkinFallback".Localize() : "SkinDragHint".Localize();
                _status.Visibility = Visibility.Visible;
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
                    loading.Visibility = Visibility.Collapsed;
            }
        };
        Closing += OnClosing;
    }

    private void OnViewerFailed(string message) => ShowStaticPreview();

    private UIElement CreateSettingsPane()
    {
        _settingsPanel.Children.Add(CreateAnimationExpander());
        _settingsPanel.Children.Add(CreateAppearanceExpander());
        _settingsPanel.Children.Add(CreateLayersExpander());

        return new ScrollViewer
        {
            Content = _settingsPanel,
            Padding = new Thickness(12, 0, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private SettingsExpander CreateAnimationExpander()
    {
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationNone", MinecraftSkinViewerAnimation.None));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationIdle", MinecraftSkinViewerAnimation.Idle));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationWalk", MinecraftSkinViewerAnimation.Walk));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationRun", MinecraftSkinViewerAnimation.Run));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationFly", MinecraftSkinViewerAnimation.Fly));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationWave", MinecraftSkinViewerAnimation.Wave));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationCrouch", MinecraftSkinViewerAnimation.Crouch));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationHit", MinecraftSkinViewerAnimation.Hit));
        _animationBox.Items.Add(CreateOption("SkinViewerAnimationSwim", MinecraftSkinViewerAnimation.Swim));
        _animationBox.SelectedIndex = 1;
        _animationBox.SelectionChanged += (_, _) => ApplyViewerSettings();
        _animationSpeed.ValueChanged += (_, _) => ApplyViewerSettings();

        var expander = new SettingsExpander
        {
            Header = "SkinViewerAnimation".Localize(),
            IsExpanded = true
        };
        expander.Items.Add(new SettingsCard
        {
            Header = "SkinViewerAnimation".Localize(),
            Content = _animationBox
        });
        expander.Items.Add(new SettingsCard
        {
            Header = "SkinViewerAnimationSpeed".Localize(),
            Content = _animationSpeed
        });
        return expander;
    }

    private SettingsExpander CreateAppearanceExpander()
    {
        _rotationEnabled.Toggled += (_, _) => ApplyViewerSettings();
        _rotationSpeed.ValueChanged += (_, _) => ApplyViewerSettings();
        _backEquipmentBox.Items.Add(CreateOption("SkinViewerCape", MinecraftSkinViewerBackEquipment.Cape));
        _backEquipmentBox.Items.Add(CreateOption("SkinViewerElytra", MinecraftSkinViewerBackEquipment.Elytra));
        _backEquipmentBox.SelectedIndex = 0;
        _backEquipmentBox.SelectionChanged += (_, _) => ApplyViewerSettings();

        var expander = new SettingsExpander
        {
            Header = "SkinViewerAppearance".Localize()
        };
        expander.Items.Add(new SettingsCard
        {
            Header = "SkinViewerRotation".Localize(),
            Content = _rotationEnabled
        });
        expander.Items.Add(new SettingsCard
        {
            Header = "SkinViewerRotationSpeed".Localize(),
            Content = _rotationSpeed
        });
        expander.Items.Add(new SettingsCard
        {
            Header = "SkinViewerBackEquipment".Localize(),
            Content = _backEquipmentBox
        });
        return expander;
    }

    private SettingsExpander CreateLayersExpander()
    {
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foreach (var _ in LayerParts)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddLayerHeader(grid, "", 0);
        for (var index = 0; index < LayerParts.Length; index++)
            AddLayerHeader(grid, LayerParts[index].ResourceKey.Localize(), index + 1);

        AddLayerLabel(grid, "SkinViewerInner".Localize(), 1);
        AddLayerLabel(grid, "SkinViewerOuter".Localize(), 2);
        for (var index = 0; index < LayerParts.Length; index++)
        {
            var (part, resourceKey) = LayerParts[index];
            var inner = CreateLayerToggle($"{resourceKey.Localize()} {"SkinViewerInner".Localize()}");
            var outer = CreateLayerToggle($"{resourceKey.Localize()} {"SkinViewerOuter".Localize()}");
            _layerToggles.Add(part, (inner, outer));
            AddLayerToggle(grid, inner, index + 1, 1);
            AddLayerToggle(grid, outer, index + 1, 2);
        }

        var expander = new SettingsExpander
        {
            Header = "SkinViewerLayers".Localize()
        };
        expander.Items.Add(new SettingsCard
        {
            Header = "SkinViewerLayers".Localize(),
            Content = grid
        });
        return expander;
    }

    private static readonly (string Part, string ResourceKey)[] LayerParts =
    [
        ("head", "SkinViewerHead"),
        ("body", "SkinViewerBody"),
        ("rightArm", "SkinViewerRightArm"),
        ("leftArm", "SkinViewerLeftArm"),
        ("rightLeg", "SkinViewerRightLeg"),
        ("leftLeg", "SkinViewerLeftLeg")
    ];

    private static ComboBoxItem CreateOption(string resourceKey, object value) => new()
    {
        Content = resourceKey.Localize(),
        Tag = value
    };

    private CheckBox CreateLayerToggle(string tooltip)
    {
        var toggle = new CheckBox
        {
            IsChecked = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(toggle, tooltip);
        toggle.Checked += (_, _) => ApplyViewerSettings();
        toggle.Unchecked += (_, _) => ApplyViewerSettings();
        return toggle;
    }

    private static void AddLayerHeader(Grid grid, string text, int column)
    {
        var block = new TextBlock
        {
            Text = text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
            Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private static void AddLayerLabel(Grid grid, string text, int row)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(block, row);
        grid.Children.Add(block);
    }

    private static void AddLayerToggle(Grid grid, CheckBox toggle, int column, int row)
    {
        Grid.SetColumn(toggle, column);
        Grid.SetRow(toggle, row);
        grid.Children.Add(toggle);
    }

    private void ToggleSettingsPane()
    {
        _settingsPane.IsPaneOpen = !_settingsPane.IsPaneOpen;
        _root.Width = _settingsPane.IsPaneOpen ? 820 : 480;
        _settingsButton.Content = (_settingsPane.IsPaneOpen ? "SkinViewerHideSettings" : "SkinViewerShowSettings").Localize();
    }

    private async void ApplyViewerSettings()
    {
        if (_settingsPanel.Visibility == Visibility.Collapsed || _cancellationSource.IsCancellationRequested)
            return;

        try
        {
            await _viewer.UpdateSettingsAsync(CreateViewerSettings(), _cancellationSource.Token);
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
        var animation = _animationBox.SelectedItem is ComboBoxItem { Tag: MinecraftSkinViewerAnimation ani }
            ? ani
            : MinecraftSkinViewerAnimation.Idle;
        var equipment = _backEquipmentBox.SelectedItem is ComboBoxItem { Tag: MinecraftSkinViewerBackEquipment eq }
            ? eq
            : MinecraftSkinViewerBackEquipment.Cape;

        return new MinecraftSkinViewerSettings(
            animation,
            _animationSpeed.Value,
            _rotationEnabled.IsOn,
            _rotationSpeed.Value,
            equipment,
            new MinecraftSkinViewerLayers(
                CreateLayer("head"),
                CreateLayer("body"),
                CreateLayer("rightArm"),
                CreateLayer("leftArm"),
                CreateLayer("rightLeg"),
                CreateLayer("leftLeg")));
    }

    private MinecraftSkinViewerLayer CreateLayer(string part)
    {
        var (inner, outer) = _layerToggles[part];
        return new MinecraftSkinViewerLayer(inner.IsChecked == true, outer.IsChecked == true);
    }

    private async void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            _cancellationSource.Cancel();
            _viewer.ViewerFailed -= OnViewerFailed;
            await _viewer.StopAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ShowStaticPreview()
    {
        if (_cancellationSource.IsCancellationRequested)
            return;

        _viewer.Visibility = Visibility.Collapsed;
        _fallbackPreview.Visibility = Visibility.Visible;
        _status.Text = "Skin3DUnavailable".Localize();
        _status.Visibility = Visibility.Visible;
    }
}
