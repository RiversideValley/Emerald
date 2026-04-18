using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Emerald.UserControls;

public sealed partial class MinecraftSettingsUC : UserControl
{
    private bool _isUpdatingOverrideControls;
    private bool _isSyncingCustomJavaToggle;
    private CancellationTokenSource? _javaRefreshCts;
    private GameSettings? _subscribedGameSettings;

    public bool ShowMainSettings
    {
        get => (bool)GetValue(ShowMainSettingsProperty);
        set => SetValue(ShowMainSettingsProperty, value);
    }

    public static readonly DependencyProperty ShowMainSettingsProperty =
        DependencyProperty.Register(nameof(ShowMainSettings), typeof(bool), typeof(MinecraftSettingsUC), new PropertyMetadata(false, OnShowMainSettingsChanged));

    public Game? Game
    {
        get => (Game?)GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    public static readonly DependencyProperty GameProperty =
        DependencyProperty.Register(nameof(Game), typeof(Game), typeof(MinecraftSettingsUC), new PropertyMetadata(null, OnGameChanged));

    public GameSettings? GameSettings
    {
        get => (GameSettings?)GetValue(GameSettingsProperty);
        set => SetValue(GameSettingsProperty, value);
    }

    public static readonly DependencyProperty GameSettingsProperty =
        DependencyProperty.Register(nameof(GameSettings), typeof(GameSettings), typeof(MinecraftSettingsUC), new PropertyMetadata(null, OnGameSettingsChanged));

    public Services.SettingsService SS { get; }

    public ObservableCollection<JavaRuntimeOptionViewModel> JavaRuntimeOptions { get; } = new();

    public bool IsRefreshingJavaPaths { get; private set; }

    public bool CanRefreshJavaPaths => !IsRefreshingJavaPaths;

    public bool HasNoDetectedJavaOptions => !JavaRuntimeOptions.Any();

    public int MinRamMb => DirectResoucres.MinRAM;

    public int MaxRamMb => DirectResoucres.MaxRAM;

    public double RamSliderValue
    {
        get => GameSettings?.MaximumRamMb ?? MinRamMb;
        set
        {
            if (GameSettings == null)
            {
                return;
            }

            var roundedToStep = (int)Math.Round(value / 64d) * 64;
            var clamped = Math.Clamp(roundedToStep, MinRamMb, MaxRamMb);
            if (GameSettings.MaximumRamMb != clamped)
            {
                GameSettings.MaximumRamMb = clamped;
            }
        }
    }

    public string SelectedJavaPathText => string.IsNullOrWhiteSpace(GameSettings?.JavaPath)
        ? "NoJavaRuntimeSelected".Localize()
        : GameSettings!.JavaPath!;

    public bool HasSelectedJavaPath => !string.IsNullOrWhiteSpace(GameSettings?.JavaPath);

    public JavaRuntimeOptionViewModel? SelectedJavaRuntimeOption
        => JavaRuntimeOptions.FirstOrDefault(option => option.IsSelected);

    public string SelectedJavaStatusText
    {
        get
        {
            if (GameSettings?.UseCustomJava != true)
            {
                return "JavaAutoManagedStatus".Localize();
            }

            if (string.IsNullOrWhiteSpace(GameSettings.JavaPath))
            {
                return "JavaCustomPathRequired".Localize();
            }

            var selectedOption = JavaRuntimeOptions.FirstOrDefault(IsCurrentJavaSelection);
            if (selectedOption != null)
            {
                return selectedOption.StatusText;
            }

            return "SelectedJavaPathUnavailable".Localize();
        }
    }

    public bool HasMissingSelectedJavaPath
        => GameSettings?.UseCustomJava == true
           && !string.IsNullOrWhiteSpace(GameSettings.JavaPath)
           && JavaRuntimeOptions.All(option => !IsCurrentJavaSelection(option) || !option.IsValid);

    public MinecraftSettingsUC()
    {
        InitializeComponent();
        SS = Ioc.Default.GetService<Services.SettingsService>();
        Loaded += MinecraftSettingsUC_Loaded;
        Unloaded += MinecraftSettingsUC_Unloaded;
        UpdateOverrideState();
    }

    private async Task PickMinecraftFolderAsync()
    {
        this.Log().LogInformation("Choosing MC path");

        var picker = new FolderPicker { CommitButtonText = "Select".Localize() };
        picker.FileTypeFilter.Add("*");

        if (DirectResoucres.Platform == "Windows")
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));
        }

        var folder = await picker.PickSingleFolderAsync();

        if (folder == null)
        {
            this.Log().LogInformation("User did not select a MC path");
            return;
        }

        var path = folder.Path;
        this.Log().LogInformation("New Minecraft path: {path}", path);
        SS.Settings.Minecraft.Path = path;

        await Ioc.Default.GetService<CoreX.Core>().InitializeAndRefresh(new(path));
        await RefreshJavaOptionsAsync();
    }

    private async void ChangePath_OnClick(object sender, RoutedEventArgs e)
        => await PickMinecraftFolderAsync();

    private void CopyPath_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = ShowMainSettings ? SS.Settings.Minecraft.Path : Path.Combine(SS.Settings.Minecraft.Path, CoreX.Core.GamesFolderName);
            var dp = new DataPackage();
            dp.SetText(path);
            Clipboard.SetContent(dp);
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to copy path");
        }
    }

    private void AdjustRam(int delta)
    {
        if (GameSettings == null)
        {
            return;
        }

        var newValue = GameSettings.MaximumRamMb + delta;
        GameSettings.MaximumRamMb = Math.Clamp(newValue, MinRamMb, MaxRamMb);
    }

    private void btnRamPlus_Click(object sender, RoutedEventArgs e) => AdjustRam(64);

    private void btnRamMinus_Click(object sender, RoutedEventArgs e) => AdjustRam(-64);

    private void GameOverrideToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingOverrideControls || Game == null)
        {
            return;
        }

        Game.UsesCustomGameSettings = GameOverrideToggle.IsOn;
        GameSettings = Game.GetEditableSettings();
        UpdateOverrideState();
    }

    private static void OnShowMainSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MinecraftSettingsUC)d;
        control.UpdateOverrideState();
        _ = control.RefreshJavaOptionsAsync();
    }

    private static void OnGameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MinecraftSettingsUC)d;
        if (e.NewValue is Game game)
        {
            control.GameSettings = game.GetEditableSettings();
        }

        control.UpdateOverrideState();
        _ = control.RefreshJavaOptionsAsync();
    }

    private static void OnGameSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MinecraftSettingsUC)d).HandleGameSettingsChanged(e.OldValue as GameSettings, e.NewValue as GameSettings);

    private void HandleGameSettingsChanged(GameSettings? oldSettings, GameSettings? newSettings)
    {
        if (oldSettings != null)
        {
            oldSettings.PropertyChanged -= GameSettings_PropertyChanged;
        }

        _subscribedGameSettings = newSettings;
        if (newSettings != null)
        {
            newSettings.PropertyChanged -= GameSettings_PropertyChanged;
            newSettings.PropertyChanged += GameSettings_PropertyChanged;
        }

        UpdateJavaSelectionState();
        Bindings.Update();
    }

    private void UpdateOverrideState()
    {
        if (GameOverrideCard == null || GameOverrideToggle == null || PerGameEditablePanel == null || UsingMainSettingsHint == null)
        {
            return;
        }

        var supportsPerGameOverride = !ShowMainSettings && Game != null;
        var isUsingCustomSettings = !supportsPerGameOverride || Game!.UsesCustomGameSettings;

        _isUpdatingOverrideControls = true;
        GameOverrideCard.Visibility = supportsPerGameOverride ? Visibility.Visible : Visibility.Collapsed;
        GameOverrideToggle.IsOn = isUsingCustomSettings;
        PerGameEditablePanel.Visibility = isUsingCustomSettings
            ? Visibility.Visible
            : Visibility.Collapsed;
        UsingMainSettingsHint.Visibility = supportsPerGameOverride && !isUsingCustomSettings
            ? Visibility.Visible
            : Visibility.Collapsed;
        _isUpdatingOverrideControls = false;

        if (Game != null)
        {
            GameSettings = Game.GetEditableSettings();
        }

        Bindings.Update();
    }

    private async void MinecraftSettingsUC_Loaded(object sender, RoutedEventArgs e)
        => await RefreshJavaOptionsAsync();

    private void MinecraftSettingsUC_Unloaded(object sender, RoutedEventArgs e)
    {
        _javaRefreshCts?.Cancel();
        _javaRefreshCts?.Dispose();
        _javaRefreshCts = null;

        if (_subscribedGameSettings != null)
        {
            _subscribedGameSettings.PropertyChanged -= GameSettings_PropertyChanged;
            _subscribedGameSettings = null;
        }
    }

    private async void RefreshJavaPaths_OnClick(object sender, RoutedEventArgs e)
        => await RefreshJavaOptionsAsync();

    private async void AddJavaFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { CommitButtonText = "Select".Localize() };
        picker.FileTypeFilter.Add("*");

        if (DirectResoucres.Platform == "Windows")
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));
        }

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null)
        {
            return;
        }

        await AddAndSelectCustomJavaAsync(folder.Path);
    }

    private async void AddJavaFile_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { CommitButtonText = "Select".Localize() };
        picker.FileTypeFilter.Add("*");

        if (DirectResoucres.Platform == "Windows")
        {
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));
        }

        var file = await picker.PickSingleFileAsync();
        if (file == null)
        {
            return;
        }

        await AddAndSelectCustomJavaAsync(file.Path);
    }

    private async void SelectJavaRuntime_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: JavaRuntimeOptionViewModel option })
        {
            return;
        }

        await TrySelectJavaRuntimeAsync(option);
    }

    private async void JavaRuntimeList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not JavaRuntimeOptionViewModel option)
        {
            return;
        }

        await TrySelectJavaRuntimeAsync(option);
    }

    private async void OpenSelectedJavaLocation_OnClick(object sender, RoutedEventArgs e)
    {
        var selectedJavaPath = GameSettings?.JavaPath;
        if (string.IsNullOrWhiteSpace(selectedJavaPath))
        {
            return;
        }

        var revealFolder = File.Exists(selectedJavaPath)
            ? Path.GetDirectoryName(selectedJavaPath)
            : selectedJavaPath;

        if (string.IsNullOrWhiteSpace(revealFolder) || !Directory.Exists(revealFolder))
        {
            Notifications().Warning("SelectedJavaPathUnavailable".Localize(), selectedJavaPath);
            return;
        }

        try
        {
            await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(revealFolder));
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to open selected Java path folder.");
            Notifications().Error("JavaDetectError".Localize(), "JavaDetectErrorMessage".Localize(), ex: ex);
        }
    }

    private async void RemoveSavedJavaRuntime_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: JavaRuntimeOptionViewModel option })
        {
            return;
        }

        RemoveSavedJavaPath(option.Path);
        if (GameSettings != null && PathsEqual(GameSettings.JavaPath, option.Path))
        {
            GameSettings.JavaPath = null;
        }

        await RefreshJavaOptionsAsync();
    }

    private void CustomJavaToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (GameSettings != null
            && sender is ToggleSwitch toggle
            && GameSettings.UseCustomJava != toggle.IsOn
            && !_isSyncingCustomJavaToggle)
        {
            _isSyncingCustomJavaToggle = true;
            try
            {
                // Keep source and control in sync before refreshing bindings.
                GameSettings.UseCustomJava = toggle.IsOn;
            }
            finally
            {
                _isSyncingCustomJavaToggle = false;
            }
        }

        UpdateJavaSelectionState();

        if (GameSettings?.UseCustomJava == true && JavaRuntimeOptions.Count == 0)
        {
            _ = RefreshJavaOptionsAsync();
        }
    }

    private async Task AddAndSelectCustomJavaAsync(string candidatePath)
    {
        if (GameSettings == null)
        {
            return;
        }

        var validation = await JavaCatalog().ValidateAsync(candidatePath);
        if (!validation.IsValid || string.IsNullOrWhiteSpace(validation.NormalizedPath))
        {
            Notifications().Warning("JavaValidationFailed".Localize(), validation.ErrorMessage ?? "JavaValidationFailedMessage".Localize());
            return;
        }

        EnsureSavedJavaPath(validation.NormalizedPath);
        GameSettings.JavaPath = validation.NormalizedPath;
        GameSettings.UseCustomJava = true;

        await RefreshJavaOptionsAsync();
    }

    private async Task TrySelectJavaRuntimeAsync(JavaRuntimeOptionViewModel option)
    {
        if (GameSettings == null)
        {
            return;
        }

        if (!option.IsValid)
        {
            Notifications().Warning("JavaValidationFailed".Localize(), "JavaValidationFailedMessage".Localize());
            return;
        }

        var validation = await JavaCatalog().ValidateAsync(option.Path);
        if (!validation.IsValid || string.IsNullOrWhiteSpace(validation.NormalizedPath))
        {
            Notifications().Warning("JavaValidationFailed".Localize(), validation.ErrorMessage ?? "JavaValidationFailedMessage".Localize());
            await RefreshJavaOptionsAsync();
            return;
        }

        GameSettings.JavaPath = validation.NormalizedPath;
        GameSettings.UseCustomJava = true;
        UpdateJavaSelectionState();
    }

    private async Task RefreshJavaOptionsAsync()
    {
        _javaRefreshCts?.Cancel();
        _javaRefreshCts?.Dispose();
        _javaRefreshCts = new CancellationTokenSource();

        IsRefreshingJavaPaths = true;
        Bindings.Update();

        try
        {
            var runtimes = await JavaCatalog().DiscoverAsync(GetCurrentMinecraftRootPath(), SS.Settings.Minecraft.SavedJavaPaths, _javaRefreshCts.Token);
            JavaRuntimeOptions.Clear();

            foreach (var runtime in runtimes)
            {
                JavaRuntimeOptions.Add(new JavaRuntimeOptionViewModel
                {
                    Path = runtime.Path,
                    DisplayPath = runtime.DisplayPath,
                    Source = runtime.Source,
                    Version = runtime.Version,
                    ErrorMessage = runtime.ErrorMessage,
                    IsCustomSaved = runtime.IsCustomSaved,
                    IsValid = runtime.IsValid
                });
            }

            UpdateJavaSelectionState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to refresh Java runtimes.");
            Notifications().Error("JavaDetectError".Localize(), "JavaDetectErrorMessage".Localize(), ex: ex);
        }
        finally
        {
            IsRefreshingJavaPaths = false;
            Bindings.Update();
        }
    }

    private void UpdateJavaSelectionState()
    {
        foreach (var option in JavaRuntimeOptions)
        {
            option.IsSelected = IsCurrentJavaSelection(option);
        }

        Bindings.Update();
    }

    private void GameSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CoreX.Models.GameSettings.MaximumRamMb))
        {
            Bindings.Update();
            return;
        }

        if (e.PropertyName == nameof(GameSettings.JavaPath) || e.PropertyName == nameof(GameSettings.UseCustomJava))
        {
            UpdateJavaSelectionState();
        }
    }

    private bool IsCurrentJavaSelection(JavaRuntimeOptionViewModel option)
        => PathsEqual(GameSettings?.JavaPath, option.Path);

    private static bool PathsEqual(string? left, string? right)
        => string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private string? GetCurrentMinecraftRootPath()
        => ShowMainSettings
            ? SS.Settings.Minecraft.Path
            : Game?.Path.BasePath ?? SS.Settings.Minecraft.Path;

    private IJavaRuntimeCatalogService JavaCatalog()
        => Ioc.Default.GetService<IJavaRuntimeCatalogService>()
           ?? throw new InvalidOperationException("Java runtime catalog service is not available.");

    private INotificationService Notifications()
        => Ioc.Default.GetService<INotificationService>()
           ?? throw new InvalidOperationException("Notification service is not available.");

    private void EnsureSavedJavaPath(string normalizedPath)
    {
        if (SS.Settings.Minecraft.SavedJavaPaths.Any(path => PathsEqual(path, normalizedPath)))
        {
            return;
        }

        SS.Settings.Minecraft.SavedJavaPaths.Add(normalizedPath);
    }

    private void RemoveSavedJavaPath(string normalizedPath)
    {
        var matches = SS.Settings.Minecraft.SavedJavaPaths
            .Where(path => PathsEqual(path, normalizedPath))
            .ToArray();

        foreach (var match in matches)
        {
            SS.Settings.Minecraft.SavedJavaPaths.Remove(match);
        }
    }
}
