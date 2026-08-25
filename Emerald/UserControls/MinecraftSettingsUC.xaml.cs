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
using Emerald.CoreX.Store;
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
    private bool _isInitializingSharedStoreLinkMode;
    private bool _isHandlingSharedStoreMigration;
    private CancellationTokenSource? _javaRefreshCts;
    private GameSettings? _subscribedGameSettings;
    private SharedStoreLinkModeOption? _selectedSharedStoreLinkModeOption;
    private readonly Dictionary<string, bool> _sharedStoreToggleSnapshot = new(StringComparer.Ordinal);

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

    public ObservableCollection<SharedStoreLinkModeOption> SharedStoreLinkModeOptions { get; } = new();

    public bool IsWindowsLinkModeVisible => OperatingSystem.IsWindows();

    public SharedStoreLinkModeOption? SelectedSharedStoreLinkModeOption
    {
        get => _selectedSharedStoreLinkModeOption;
        set
        {
            if (ReferenceEquals(_selectedSharedStoreLinkModeOption, value))
            {
                return;
            }

            _selectedSharedStoreLinkModeOption = value;
            if (_isInitializingSharedStoreLinkMode || value == null)
            {
                return;
            }

            var settingsService = Ioc.Default.GetService<IStoreSharedContentSettingsService>();
            if (settingsService == null)
            {
                return;
            }

            settingsService.Settings.WindowsLinkMode = value.Value;
            settingsService.Save();

            if (OperatingSystem.IsWindows() && value.Value == StoreLinkMode.SymbolicLink)
            {
                _ = ShowWindowsSymlinkWarningAsync();
            }
        }
    }

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
        InitializeSharedStoreLinkModeOptions();
        Loaded += MinecraftSettingsUC_Loaded;
        Unloaded += MinecraftSettingsUC_Unloaded;
        UpdateOverrideState();
    }

    private void InitializeSharedStoreLinkModeOptions()
    {
        SharedStoreLinkModeOptions.Clear();
        SharedStoreLinkModeOptions.Add(new SharedStoreLinkModeOption(StoreLinkMode.HardLink, "Hard link"));
        SharedStoreLinkModeOptions.Add(new SharedStoreLinkModeOption(StoreLinkMode.SymbolicLink, "Symbolic link"));
        SharedStoreLinkModeOptions.Add(new SharedStoreLinkModeOption(StoreLinkMode.Copy, "Copy"));

        var settingsService = Ioc.Default.GetService<IStoreSharedContentSettingsService>();
        var selectedMode = settingsService?.Settings.WindowsLinkMode ?? StoreLinkMode.HardLink;

        _isInitializingSharedStoreLinkMode = true;
        SelectedSharedStoreLinkModeOption = SharedStoreLinkModeOptions.FirstOrDefault(option => option.Value == selectedMode)
                                            ?? SharedStoreLinkModeOptions.FirstOrDefault();
        _isInitializingSharedStoreLinkMode = false;
    }

    private async Task ShowWindowsSymlinkWarningAsync()
    {
        if (XamlRoot == null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Windows symbolic links",
            Content = "Symbolic links may require Developer Mode or administrator permission on Windows. If linking fails, Emerald will fall back to copying the file.",
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
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
        var core = Ioc.Default.GetService<CoreX.Core>();
        try
        {
            await core.InitializeLocalAsync(new(path));
            SS.Settings.Minecraft.Path = path;
            _ = core.RefreshVersionCatalogAsync();
            await RefreshJavaOptionsAsync();
        }
        catch (InvalidOperationException ex)
        {
            Ioc.Default.GetService<CoreX.Notifications.INotificationService>()
                ?.Warning("Minecraft path unchanged", ex.Message);
        }
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

        CaptureSharedStoreToggleSnapshot(newSettings);
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

    private void CaptureSharedStoreToggleSnapshot(GameSettings? settings)
    {
        _sharedStoreToggleSnapshot.Clear();
        if (settings == null)
        {
            return;
        }

        foreach (var propertyName in SharedStoreToggleProperties)
        {
            _sharedStoreToggleSnapshot[propertyName] = GetSharedStoreToggleValue(settings, propertyName);
        }
    }

    private static readonly string[] SharedStoreToggleProperties =
    [
        nameof(GameSettings.UseSharedStoreModsPath),
        nameof(GameSettings.UseSharedStoreResourcePacksPath),
        nameof(GameSettings.UseSharedStoreDataPacksPath),
        nameof(GameSettings.UseSharedStoreShaderPacksPath)
    ];

    private static bool IsSharedStoreToggleProperty(string? propertyName)
        => propertyName != null && SharedStoreToggleProperties.Contains(propertyName);

    private static bool GetSharedStoreToggleValue(GameSettings settings, string propertyName)
        => propertyName switch
        {
            nameof(GameSettings.UseSharedStoreModsPath) => settings.UseSharedStoreModsPath,
            nameof(GameSettings.UseSharedStoreResourcePacksPath) => settings.UseSharedStoreResourcePacksPath,
            nameof(GameSettings.UseSharedStoreDataPacksPath) => settings.UseSharedStoreDataPacksPath,
            nameof(GameSettings.UseSharedStoreShaderPacksPath) => settings.UseSharedStoreShaderPacksPath,
            _ => false
        };

    private static (StoreContentType ContentType, string InstallFolderName, string DisplayName) ResolveSharedStoreToggle(string propertyName)
        => propertyName switch
        {
            nameof(GameSettings.UseSharedStoreModsPath) => (StoreContentType.Mod, "mods", "mods"),
            nameof(GameSettings.UseSharedStoreResourcePacksPath) => (StoreContentType.ResourcePack, "resourcepacks", "resource packs"),
            nameof(GameSettings.UseSharedStoreDataPacksPath) => (StoreContentType.DataPack, "datapacks", "data packs"),
            nameof(GameSettings.UseSharedStoreShaderPacksPath) => (StoreContentType.Shader, "shaderpacks", "shader packs"),
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, null)
        };

    private async Task HandleSharedStoreToggleMigrationAsync(string propertyName, bool enabled)
    {
        if (_isHandlingSharedStoreMigration)
        {
            return;
        }

        var sharedContentService = Ioc.Default.GetService<IStoreSharedContentService>();
        var core = Ioc.Default.GetService<CoreX.Core>();
        if (sharedContentService == null || core == null)
        {
            return;
        }

        var affectedGames = GetAffectedSharedStoreGames(core).ToArray();
        if (affectedGames.Length == 0)
        {
            return;
        }

        var (contentType, installFolderName, displayName) = ResolveSharedStoreToggle(propertyName);
        var plans = new List<StoreSharedContentMigrationPlan>();
        foreach (var game in affectedGames)
        {
            plans.Add(await sharedContentService.CreateMigrationPlanAsync(
                game,
                contentType,
                enabled,
                installFolderName));
        }

        var summary = sharedContentService.SummarizeMigrationPlans(plans);
        if (!summary.HasWork)
        {
            return;
        }

        _isHandlingSharedStoreMigration = true;
        try
        {
            var action = enabled
                ? await ShowEnableSharedStoreMigrationDialogAsync(displayName, summary)
                : await ShowDisableSharedStoreMigrationDialogAsync(displayName, summary);

            foreach (var plan in plans)
            {
                await sharedContentService.ApplyMigrationAsync(plan, action);
            }

            Ioc.Default.GetService<CoreX.Core>()?.SaveGames();
        }
        finally
        {
            _isHandlingSharedStoreMigration = false;
        }
    }

    private IEnumerable<Game> GetAffectedSharedStoreGames(CoreX.Core core)
    {
        if (!ShowMainSettings && Game != null)
        {
            yield return Game;
            yield break;
        }

        foreach (var game in core.Games.Where(game => !game.UsesCustomGameSettings))
        {
            yield return game;
        }
    }

    private async Task<StoreSharedContentMigrationAction> ShowEnableSharedStoreMigrationDialogAsync(
        string displayName,
        StoreSharedContentMigrationSummary summary)
    {
        if (XamlRoot == null)
        {
            return StoreSharedContentMigrationAction.OnlyFutureInstalls;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Enable shared {displayName}?",
            Content = BuildEnableMigrationDialogText(summary),
            PrimaryButtonText = "Convert tracked",
            SecondaryButtonText = "Convert all",
            CloseButtonText = "Future only",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => StoreSharedContentMigrationAction.ConvertTrackedFiles,
            ContentDialogResult.Secondary => StoreSharedContentMigrationAction.ConvertAllCompatibleFiles,
            _ => StoreSharedContentMigrationAction.OnlyFutureInstalls
        };
    }

    private async Task<StoreSharedContentMigrationAction> ShowDisableSharedStoreMigrationDialogAsync(
        string displayName,
        StoreSharedContentMigrationSummary summary)
    {
        if (XamlRoot == null)
        {
            return StoreSharedContentMigrationAction.LeaveExistingLinks;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Disable shared {displayName}?",
            Content = BuildDisableMigrationDialogText(summary),
            PrimaryButtonText = "Make copies",
            SecondaryButtonText = "Remove",
            CloseButtonText = "Keep links",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => StoreSharedContentMigrationAction.MaterializeFiles,
            ContentDialogResult.Secondary => StoreSharedContentMigrationAction.RemoveSharedInstalls,
            _ => StoreSharedContentMigrationAction.LeaveExistingLinks
        };
    }

    private static string BuildEnableMigrationDialogText(StoreSharedContentMigrationSummary summary)
    {
        var text = BuildMigrationSummaryText(summary);
        return string.Join(
            Environment.NewLine,
            text,
            "",
            "Convert tracked: move only files Emerald already tracks.",
            "Convert all: also import compatible manual files.",
            "Future only: leave existing files alone.");
    }

    private static string BuildDisableMigrationDialogText(StoreSharedContentMigrationSummary summary)
    {
        var text = BuildMigrationSummaryText(summary);
        return string.Join(
            Environment.NewLine,
            text,
            "",
            "Make copies: replace links with normal files.",
            "Remove: delete shared installs from instances.",
            "Keep links: leave current links as-is.");
    }

    private static string BuildMigrationSummaryText(StoreSharedContentMigrationSummary summary)
    {
        var lines = new List<string>();
        if (summary.TrackedConvertibleCount > 0)
        {
            lines.Add($"{summary.TrackedConvertibleCount} tracked file(s) can be converted safely.");
        }

        if (summary.SharedInstallCount > 0)
        {
            lines.Add($"{summary.SharedInstallCount} shared install(s) already exist.");
        }

        if (summary.UntrackedFileCount > 0)
        {
            lines.Add($"{summary.UntrackedFileCount} untracked/manual file(s) were found.");
        }

        if (summary.HashMismatchCount > 0)
        {
            lines.Add($"{summary.HashMismatchCount} tracked file(s) appear modified or lack a usable Modrinth hash.");
        }

        if (summary.BrokenOrMissingCount > 0)
        {
            lines.Add($"{summary.BrokenOrMissingCount} broken or missing shared file(s) need repair.");
        }

        lines.Add("Emerald will not change untracked, modified, or broken files unless you choose an option that includes them.");
        return string.Join(Environment.NewLine, lines);
    }

    private void GameSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CoreX.Models.GameSettings.MaximumRamMb))
        {
            Bindings.Update();
            return;
        }

        if (e.PropertyName == nameof(GameSettings.SharedMinecraftFoldersStatus))
        {
            Bindings.Update();
            return;
        }

        if (e.PropertyName == nameof(GameSettings.SharedStoreFoldersStatus))
        {
            Bindings.Update();
            return;
        }

        if (IsSharedStoreToggleProperty(e.PropertyName) && GameSettings != null)
        {
            var propertyName = e.PropertyName!;
            var newValue = GetSharedStoreToggleValue(GameSettings, propertyName);
            var hadOldValue = _sharedStoreToggleSnapshot.TryGetValue(propertyName, out var oldValue);
            _sharedStoreToggleSnapshot[propertyName] = newValue;
            Bindings.Update();

            if (hadOldValue && oldValue != newValue)
            {
                _ = HandleSharedStoreToggleMigrationAsync(propertyName, newValue);
            }

            return;
        }

        if (e.PropertyName == nameof(GameSettings.UseSharedRuntimePath))
        {
            _ = RefreshJavaOptionsAsync();
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
    {
        if (ShowMainSettings)
        {
            return SS.Settings.Minecraft.Path;
        }

        if (Game?.EffectiveSettings.UseSharedRuntimePath == true
            && !string.IsNullOrWhiteSpace(Game.SharedMinecraftBasePath))
        {
            return Game.SharedMinecraftBasePath;
        }

        return Game?.Path.BasePath ?? SS.Settings.Minecraft.Path;
    }

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

public sealed class SharedStoreLinkModeOption(StoreLinkMode value, string displayName)
{
    public StoreLinkMode Value { get; } = value;

    public string DisplayName { get; } = displayName;
}
