using System;
using System.Reflection;
using CmlLib.Core;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.CrashHandling;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.Helpers;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Serilog.Sinks.File;
using Microsoft.UI.Dispatching;
using Uno.Extensions;
using Uno.Extensions.Hosting;
using Uno.Resizetizer;
using Windows.ApplicationModel.DataTransfer;
using Launcher = Windows.System.Launcher;

namespace Emerald;

/// <summary>
/// Hosts the Uno application composition root, startup flow, and crash handling.
/// </summary>
public partial class App : Application
{
    private const string CurrentReleaseNotes = """
What's new
- Emerald now stores Windows app data in the app's local ApplicationData folder.
- Minecraft instances now use the Instances folder by default.
- Release notes now appear once after a fresh install or app update.

Notes
- Existing custom Minecraft paths and saved instances are left where they are.
- You can still change the Minecraft path from Settings.
""";

    private Services.SettingsService SS = null!;
    private readonly CrashCoordinator _crashCoordinator;
    private Task? _startupTask;
    private int _normalShutdownStarted;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        CrashFaultInjection.ConfigureFromArguments(Environment.GetCommandLineArgs().Skip(1));
        _crashCoordinator = CrashBootstrap.Initialize();
        this.UnhandledException += App_UnhandledException;

        try
        {
            this.InitializeComponent();
        }
        catch (Exception exception)
        {
            _crashCoordinator.CaptureAndTerminate(exception, "App constructor");
        }
    }

    public Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }
    public CrashCoordinator CrashCoordinator => _crashCoordinator;

    #region  Services

    private void ConfigureAuthServices(IServiceCollection services)
    {
        var elyByOptions = new CoreX.Services.Auth.ElyBy.ElyByOAuthOptions(
            GetBuildMetadata("Emerald.ElyByClientId"),
            GetBuildMetadata("Emerald.ElyByClientSecret"),
            GetBuildMetadata("Emerald.ElyByRedirectUri"));

        // Ely.by values are injected by CI or Directory.Build.local.props and
        // intentionally never live in tracked source.
        services.AddSingleton<CoreX.Services.Auth.ElyBy.ElyByOAuthOptions>(_ =>
            elyByOptions);
        services.AddSingleton<CoreX.Services.Auth.ElyBy.IElyByAuthClient>(provider =>
            new CoreX.Services.Auth.ElyBy.ElyByAuthClient(
                provider.GetRequiredService<ILogger<CoreX.Services.Auth.ElyBy.ElyByAuthClient>>(),
                provider.GetRequiredService<CoreX.Services.Auth.ElyBy.ElyByOAuthOptions>()));
        services.AddSingleton<CoreX.Services.Auth.ElyBy.IElyByAccountStore, CoreX.Services.Auth.ElyBy.ElyByAccountStore>();
        services.AddSingleton<CoreX.Services.Auth.OAuth.ISystemBrowserLauncher>(provider =>
        {
            var dispatcherQueue = MainWindow?.DispatcherQueue
                                  ?? DispatcherQueue.GetForCurrentThread()
                                  ?? throw new InvalidOperationException("A DispatcherQueue is required for Ely.by browser authentication.");

            return new Services.UnoSystemBrowserLauncher(dispatcherQueue);
        });
        services.AddSingleton<CoreX.Services.Auth.OAuth.IBrowserOAuthBroker>(provider =>
            new CoreX.Services.Auth.OAuth.LoopbackBrowserOAuthBroker(
                provider.GetRequiredService<ILogger<CoreX.Services.Auth.OAuth.LoopbackBrowserOAuthBroker>>(),
                provider.GetRequiredService<CoreX.Services.Auth.OAuth.ISystemBrowserLauncher>()));

        //authLib
        services.AddSingleton<CoreX.Services.Auth.Authlib.IAuthlibInjectorService>(provider =>
            new CoreX.Services.Auth.Authlib.AuthlibInjectorService(
                provider.GetRequiredService<ILogger<CoreX.Services.Auth.Authlib.AuthlibInjectorService>>(),
                Path.Combine(DirectResoucres.LocalDataPath, "authlib-injector")));

        services.AddSingleton(new CoreX.Services.Auth.AccountProviderPolicyOptions
        {
            RequireMicrosoftForOfflineAccounts = false,
            RequireMicrosoftForElyByAccounts = false
        });
        services.AddEmeraldAccountProviders(GetBuildMetadata("Emerald.MSFTClientId"));

        //Accounts
        services.AddSingleton<CoreX.Services.IAccountService>(provider =>
        {
            var dispatcherQueue = MainWindow?.DispatcherQueue
                                  ?? DispatcherQueue.GetForCurrentThread()
                                  ?? throw new InvalidOperationException("A DispatcherQueue is required for the account service.");

            return new CoreX.Services.AccountService(
                provider.GetRequiredService<ILogger<CoreX.Services.AccountService>>(),
                provider.GetRequiredService<Services.IBaseSettingsService>(),
                new Services.DispatcherQueueUiDispatcher(dispatcherQueue),
                provider.GetServices<CoreX.Services.Auth.IAccountProvider>(),
                Path.Combine(DirectResoucres.LocalDataPath, "accounts", "cml_accounts.json"),
                notificationService: provider.GetRequiredService<CoreX.Notifications.INotificationService>());
        });
    }

    private static string GetBuildMetadata(string key)
        => typeof(App).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value ?? string.Empty;

    private void ConfigureCoreServices(IServiceCollection services)
    {
        services.AddSingleton<CoreX.Core>();

        services.AddSingleton(_ =>
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Emerald-Launcher/1.0");
            return client;
        });
        services.AddSingleton<INetworkCapabilityService, NetworkCapabilityService>();
        services.AddSingleton<IDownloadActivityService, DownloadActivityService>();
        services.AddSingleton<DownloadTimeouts>();
        services.AddSingleton<CoreX.Services.IUiDispatcher>(_ =>
            new Services.DispatcherQueueUiDispatcher(MainWindow.DispatcherQueue));
        services.AddSingleton<IInstallationStateStore, InstallationStateStore>();
        services.AddSingleton<VerifiedGameInstaller>();
        services.AddSingleton<IInstanceInstallationService, InstanceInstallationService>();

        services.AddSingleton<CoreX.Runtime.IGameRuntimeService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<GameRuntimeService>>();
            var notificationService = provider.GetRequiredService<CoreX.Notifications.INotificationService>();
            var accountService = provider.GetRequiredService<CoreX.Services.IAccountService>();
            var runtimeSettings = provider.GetRequiredService<CoreX.Runtime.IGameRuntimeSettings>();
            var dispatcherQueue = MainWindow?.DispatcherQueue
                                  ?? DispatcherQueue.GetForCurrentThread()
                                  ?? throw new InvalidOperationException("A DispatcherQueue is required for the game runtime service.");

            return new GameRuntimeService(
                logger,
                notificationService,
                accountService,
                runtimeSettings,
                new Services.DispatcherQueueUiDispatcher(dispatcherQueue),
                provider.GetRequiredService<IInstanceInstallationService>(),
                provider.GetRequiredService<INetworkCapabilityService>());
        });

        //Mod Loaders
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Fabric>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Forge>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.NeoForge>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.LiteLoader>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Quilt>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Optifine>();

        services.AddTransient<CoreX.Installers.ModLoaderRouter>();
        
        // Options.txt
        services.AddTransient<CoreX.GameOptions.IMinecraftOptionsService,
            CoreX.GameOptions.MinecraftOptionsService>();
    }

    private void ConfigureStoreServices(IServiceCollection services)
    {
        //Stores
        services.AddTransient<ModStore>();
        services.AddTransient<ResourcePackStore>();
        services.AddTransient<ShaderStore>();
        services.AddTransient<DataPackStore>();
        services.AddTransient<ModPackStore>();
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ModStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ResourcePackStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ShaderStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<DataPackStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ModPackStore>());
        services.AddSingleton<IStoreFileLinkService, StoreFileLinkService>();
        services.AddSingleton<IStoreInstallRecordRepository, StoreInstallRecordRepository>();
        services.AddSingleton<IStoreSharedContentSettingsService, StoreSharedContentSettingsService>();
        services.AddSingleton<IStoreSharedContentService, StoreSharedContentService>();
        services.AddTransient<IGameStoreContentService, GameStoreContentService>();
        services.AddTransient<CoreX.Modpacks.IMrPackReader, CoreX.Modpacks.MrPackReader>();
        services.AddTransient<CoreX.Modpacks.IMrPackFileInstaller, CoreX.Modpacks.MrPackFileInstaller>();
        services.AddTransient<CoreX.Modpacks.IModpackInstanceCreationService, CoreX.Modpacks.ModpackInstanceCreationService>();
    }

    private void ConfigureSettingsServices(IServiceCollection services)
    {
        //Settings
        services.AddSingleton<Services.SettingsService>();
        services.AddSingleton<Services.IBaseSettingsService, Services.BaseSettingsService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<Services.BaseSettingsService>>();
            var path = Path.Combine(DirectResoucres.LocalDataPath, "settings");
            return new BaseSettingsService(logger, path);
        });
        services.AddSingleton<CoreX.Services.IMinecraftBaseSettingsService, CoreX.Services.MinecraftBaseSettingsService>();
        services.AddSingleton<Services.IAppUpdateService, Services.AppUpdateService>();
        services.AddSingleton<CoreX.Services.IGlobalGameSettingsService, CoreX.Services.GlobalGameSettingsService>();
        services.AddSingleton<CoreX.Runtime.IGameRuntimeSettings, Services.GameRuntimeSettingsAdapter>();
        services.AddSingleton<CoreX.Services.IJavaRuntimeProbe, CoreX.Services.ProcessJavaRuntimeProbe>();
        services.AddSingleton<CoreX.Services.IJavaRuntimeCatalogService, CoreX.Services.JavaRuntimeCatalogService>();
    }

    private void ConfigureUiServices(IServiceCollection services)
    {
        //Notifications
        services.AddSingleton<CoreX.Notifications.INotificationService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<CoreX.Notifications.NotificationService>>();
            var inner = new CoreX.Notifications.NotificationService(logger);
            return new DispatchedNotificationService(inner, MainWindow.DispatcherQueue);
        });

        //ViewModels
        services.AddSingleton<ViewModels.GamesPageViewModel>();
        services.AddTransient<ViewModels.NotificationListViewModel>();
        services.AddSingleton<ViewModels.AccountsPageViewModel>();
        services.AddTransient<ViewModels.LogsPageViewModel>();
        services.AddTransient<ViewModels.CrashReportsPageViewModel>();
        services.AddTransient<ViewModels.ModrinthStorePageViewModel>();
        services.AddTransient<ViewModels.GameOptionsViewModel>();
    }
    
    #endregion
    
    /// <summary>
    /// Registers the maintained services and viewmodels used by the active Uno shell.
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_crashCoordinator);
        services.AddSingleton<ICrashReportStore>(_crashCoordinator.Store);
        ConfigureCoreServices(services);
        ConfigureSettingsServices(services);
        ConfigureAuthServices(services);
        ConfigureStoreServices(services);
        ConfigureUiServices(services);
    }

    /// <summary>
    /// Builds the app host, loads persisted state, and activates the main shell window.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            CrashFaultInjection.ConfigureFromActivationArguments(args.Arguments);
#if DEBUG
            if (CrashFaultInjection.IsRequested("WinAppStartup"))
            {
                throw new NotImplementedException("Intentional WinAppSDK startup crash test.");
            }
#endif
            OnLaunchedCore(args);
        }
        catch (Exception exception)
        {
            _crashCoordinator.CaptureAndTerminate(exception, "OnLaunched");
        }
    }

    private void OnLaunchedCore(LaunchActivatedEventArgs args)
    {
        var logPath = _crashCoordinator.ApplicationLogPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        }
        catch
        {
        }

        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseSerilog(true, configureLogger: x => x
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft.UI", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("Uno", Serilog.Events.LogEventLevel.Warning)
                    .WriteTo.File(logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}"))
                .ConfigureServices((context, services) => ConfigureServices(services))
            );

        MainWindow = builder.Window;
#if DEBUG
        if (!CrashFaultInjection.DisableStudio)
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon("Assets/Icon.ico");

        // The window and blank root exist before the host and MainPage. This is the
        // only safe place to present recovery after an early startup failure.
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;
            this.Log().LogDebug("Created a new root navigation frame for the main window.");
        }

        MainWindow.Activate();
        MainWindow.Closed += MainWindow_Closed;
        CrashBootstrap.RegisterNormalShutdown(() =>
        {
            CompleteNormalShutdown();
            Environment.Exit(0);
        });
#if !WINDOWS
        MacApplicationTerminationObserver.Register(CompleteNormalShutdown);
#endif
        _startupTask ??= ContinueStartupAsync(builder, args, rootFrame);
    }

    private async Task ContinueStartupAsync(IApplicationBuilder builder, LaunchActivatedEventArgs args, Frame rootFrame)
    {
        try
        {
            await WaitForRootAsync(rootFrame);
            var showRecovery = await ShowRecoveryIfNeededAsync(rootFrame);
            if (showRecovery && !_normalStartupChosen) return;
            _crashCoordinator.MarkNormalStartupAttempted();
            BuildHost(builder);
            await InitializeApplicationAsync(args, rootFrame, showRecovery);
        }
        catch (Exception exception)
        {
            _crashCoordinator.CaptureAndTerminate(exception, "Startup");
        }
    }

    private async Task<bool> ShowRecoveryIfNeededAsync(Frame rootFrame)
    {
        var pendingReport = _crashCoordinator.GetUnacknowledgedReports().FirstOrDefault();
        var shouldShow = !CrashFaultInjection.IsArmed
            && (pendingReport is not null || _crashCoordinator.IsRecoveryMode);
        if (!shouldShow) return false;

        await ShowPendingCrashAtStartupAsync(rootFrame,
            pendingReport ?? _crashCoordinator.GetReports().FirstOrDefault());
        return true;
    }

    private void BuildHost(IApplicationBuilder builder)
    {
        Host = builder.Build();
        Ioc.Default.ConfigureServices(Host.Services);
        NativeDispatcherFatalLoggerProvider.AttachHost(Host.Services.GetRequiredService<ILoggerFactory>());
        _crashCoordinator.SetLogger(Host.Services.GetRequiredService<ILogger<CrashCoordinator>>());
        this.Log().LogInformation("Application host built successfully. LogPath: {LogPath}.", _crashCoordinator.ApplicationLogPath);
    }

    private async Task InitializeApplicationAsync(
        LaunchActivatedEventArgs args,
        Frame rootFrame,
        bool showedRecovery)
    {
        LoadSettings();
        await InitializeCoreAsync();
        await InitializeMainShellAsync(args, rootFrame);
        MarkShellReady();
        await ShowOptionalStartupDialogsAsync(showedRecovery);
    }

    private void LoadSettings()
    {
        SS = Ioc.Default.GetRequiredService<Services.SettingsService>();
        SS.LoadData();
        this.Log().LogInformation("Application settings loaded.");
    }

    private async Task InitializeCoreAsync()
    {
        var core = Ioc.Default.GetRequiredService<CoreX.Core>();
        var configuredMinecraftPath = SS.Settings.Minecraft.Path;
        var startupMinecraftPath = string.IsNullOrWhiteSpace(configuredMinecraftPath)
            ? new MinecraftPath()
            : new MinecraftPath(configuredMinecraftPath);

        await core.InitializeLocalAsync(startupMinecraftPath);
        if (!_crashCoordinator.IsRecoveryMode)
        {
            _ = RunBackgroundStartupTaskAsync(
                () => core.RefreshVersionCatalogAsync(),
                "Minecraft version catalog refresh");
        }

        var accountService = Ioc.Default.GetRequiredService<CoreX.Services.IAccountService>();
        _ = RunBackgroundStartupTaskAsync(
            () => accountService.InitializeAsync(),
            "Account service initialization");
    }

    private async Task InitializeMainShellAsync(LaunchActivatedEventArgs args, Frame rootFrame)
    {
        if (rootFrame.Content is not null && rootFrame.Content is not MainPage)
        {
            rootFrame.Content = null;
        }

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
            this.Log().LogInformation("Navigated to the main page.");
        }

        MainWindow!.Activate();
        if (rootFrame.Content is MainPage mainPage)
        {
            await mainPage.ShellReady;
        }
    }

    private void MarkShellReady()
    {
        _crashCoordinator.MarkStartupComplete();
        CrashFaultInjection.WriteCheckpoint("ShellReady");
        this.Log().LogInformation("Main shell is ready.");
        _ = Task.Run(_crashCoordinator.EnrichNativeDiagnostics);
    }

    private async Task ShowOptionalStartupDialogsAsync(bool showedRecovery)
    {
        if (_crashCoordinator.IsRecoveryMode) return;
        if (!showedRecovery) await ShowReleaseNotesAtStartupAsync();
        await CheckForUpdatesAtStartupAsync();
    }

    private bool _normalStartupChosen;

    private static async Task WaitForRootAsync(FrameworkElement root)
    {
        if (root.XamlRoot is not null)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object sender, RoutedEventArgs args)
        {
            root.Loaded -= OnLoaded;
            completion.TrySetResult();
        }

        root.Loaded += OnLoaded;
        if (root.XamlRoot is not null)
        {
            root.Loaded -= OnLoaded;
            completion.TrySetResult();
        }

        // Loaded is the readiness signal; the timeout is only a degraded-mode
        // escape hatch for a platform that cannot provide XamlRoot. ContentDialog
        // will then fail predictably and the inline recovery panel remains usable.
        await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// Persists settings when the main window closes.
    /// </summary>
    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        CompleteNormalShutdown();
    }

    public void CompleteNormalShutdown()
    {
        if (Interlocked.Exchange(ref _normalShutdownStarted, 1) != 0)
        {
            return;
        }

        try
        {
            if (SS is not null)
            {
                SS.FlushPendingSave();
            }

            _crashCoordinator.MarkCleanExit();
        }
        catch (Exception exception)
        {
            _crashCoordinator.CaptureAndTerminate(exception, "Normal shutdown");
        }
    }

    private async Task RunBackgroundStartupTaskAsync(Func<Task> operation, string source)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            _crashCoordinator.ObserveBackgroundFault(exception, source);
            try
            {
                this.Log().LogError(exception, "Background startup operation failed: {Source}.", source);
            }
            catch
            {
                // Logging is best effort for a recoverable background failure.
            }
        }
    }

    private async Task ShowReleaseNotesAtStartupAsync()
    {
        try
        {
            var currentVersion = DirectResoucres.PackageVersion;
            if (string.IsNullOrWhiteSpace(currentVersion)
                || string.Equals(
                    SS.Settings.App.Updates.LastShownReleaseNotesVersion,
                    currentVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = CreateReleaseNotesDialog();
            await dialog.ShowAsync();

            SS.Settings.App.Updates.LastShownReleaseNotesVersion = currentVersion;
            SS.SaveData();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning(ex, "Failed to show startup release notes.");
        }
    }

    private ContentDialog CreateReleaseNotesDialog()
    {
        var content = new StackPanel
        {
            Spacing = 12,
            MaxWidth = 720
        };

        content.Children.Add(new TextBlock
        {
            Text = $"{DirectResoucres.PublicVersion} ({DirectResoucres.PackageVersion})",
            Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style,
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(new TextBlock
        {
            Text = CurrentReleaseNotes,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap
        });

        return new ScrollViewer
        {
            Content = content,
            Padding = new(12)
        }.ToContentDialog("ReleaseNotes".Localize(), "Close".Localize());
    }

    private async Task CheckForUpdatesAtStartupAsync()
    {
        try
        {
            if (!SS.Settings.App.Updates.CheckAtStartup)
            {
                return;
            }

            var updateService = Ioc.Default.GetService<Services.IAppUpdateService>();
            var notificationService = Ioc.Default.GetService<CoreX.Notifications.INotificationService>();

            if (updateService is null || notificationService is null)
            {
                return;
            }

            var result = await updateService.CheckForUpdatesAsync(SS.Settings.App.Updates.PreferredChannel);
            if (result.Status == Services.AppUpdateStatus.UpdateAvailable)
            {
                var message = $"{result.LatestPublicVersion ?? result.LatestPackageVersion?.ToString() ?? "-"}";
                notificationService.Info("UpdateAvailable".Localize(), message);
            }
        }
        catch (Exception ex)
        {
            this.Log().LogWarning(ex, "Startup update check failed.");
        }
    }

    public new static App Current => (App)Application.Current;

    #region UnhandledExceptions

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _crashCoordinator.CaptureAndTerminate(e.Exception, "UI.UnhandledException");
    }

    private async Task ShowPendingCrashAtStartupAsync(FrameworkElement root, CrashRecord? record)
    {
        try
        {
            var context = CreateRecoveryDialog(root, record);
            WireRecoveryDialog(context, root);
            await context.Dialog.ShowAsync();
            await ShowEmergencyPanelIfNeededAsync(root, record);
        }
        catch (Exception exception)
        {
            this.Log().LogWarning(exception, "Previous-crash recovery dialog failed to open.");
            await ShowEmergencyRecoveryPanelAsync(root, record);
        }
    }

    private RecoveryDialogContext CreateRecoveryDialog(FrameworkElement root, CrashRecord? record)
    {
        var openLogsButton = new Button
        {
            Content = "OpenCrashLogs".Localize(),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var details = CreateRecoveryDetails(record);
        details.Visibility = Visibility.Collapsed;
        var status = new TextBlock { TextWrapping = TextWrapping.WrapWholeWords };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = GetRecoveryDescription(record),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        content.Children.Add(new TextBlock
        {
            Text = record is null ? string.Empty
                : $"{record.AppVersion} · {record.Platform} · {record.OccurredUtc.ToLocalTime():g}",
            TextWrapping = TextWrapping.WrapWholeWords
        });
        content.Children.Add(openLogsButton);
        content.Children.Add(details);
        content.Children.Add(status);

        var dialog = new ContentDialog
        {
            Title = (_crashCoordinator.IsRecoveryMode ? "RecoveryMode" : "EmeraldCrashDetected").Localize(),
            Content = content,
            PrimaryButtonText = "ViewCrashReport".Localize(),
            SecondaryButtonText = "ReportToGitHub".Localize(),
            CloseButtonText = (_crashCoordinator.IsRecoveryMode ? "TryNormalStartup" : "Continue").Localize(),
            IsPrimaryButtonEnabled = record is not null,
            IsSecondaryButtonEnabled = record is not null,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root.XamlRoot
        };
        return new RecoveryDialogContext(dialog, openLogsButton, details, status, record);
    }

    private string GetRecoveryDescription(CrashRecord? record)
    {
        if (_crashCoordinator.IsRecoveryMode) return "RecoveryModeDescription".Localize();
        if (record?.Kind == CrashRecordKind.UnexpectedShutdown) return "UnexpectedShutdownDescription".Localize();
        return "CrashRecoveryDescription".Localize();
    }

    private void WireRecoveryDialog(RecoveryDialogContext context, FrameworkElement root)
    {
        context.Dialog.PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            ViewRecoveryDetails(context);
        };
        context.Dialog.SecondaryButtonClick += async (_, args) =>
            await ReportFromRecoveryDialogAsync(context, args);
        context.Dialog.CloseButtonClick += (_, _) => ContinueFromRecovery(context);
        context.OpenLogsButton.Click += async (_, _) =>
            await OpenLogsFromRecoveryAsync(context.Record);
        context.Dialog.Opened += (_, _) =>
        {
            CrashFaultInjection.WriteCheckpoint($"Recovery dialog opened: {context.Record?.Id ?? "none"}");
            CrashFaultInjection.ExerciseRecoveryActions(root.DispatcherQueue,
                () => ViewRecoveryDetails(context),
                () =>
                {
                    ContinueFromRecovery(context);
                    context.Dialog.Hide();
                });
        };
    }

    private void ViewRecoveryDetails(RecoveryDialogContext context)
    {
        Acknowledge(context.Record);
        var show = context.Details.Visibility != Visibility.Visible;
        context.Details.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        context.Dialog.PrimaryButtonText = (show ? "HideCrashDetails" : "ViewCrashReport").Localize();
        CrashFaultInjection.WriteCheckpoint("Recovery details viewed");
    }

    private void ContinueFromRecovery(RecoveryDialogContext context)
    {
        Acknowledge(context.Record);
        _normalStartupChosen = true;
    }

    private void Acknowledge(CrashRecord? record)
    {
        if (record is not null) _crashCoordinator.Acknowledge(record.Id);
    }

    private async Task ReportFromRecoveryDialogAsync(
        RecoveryDialogContext context,
        ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        Acknowledge(context.Record);
        context.Dialog.IsSecondaryButtonEnabled = false;
        try
        {
            if (context.Record is not null) await ReportCrashOnGitHubAsync(context.Record);
        }
        catch (Exception exception)
        {
            context.Status.Text = "CouldNotOpenGitHubReport".Localize();
            this.Log().LogWarning(exception, "Could not open GitHub from recovery.");
        }
        finally
        {
            context.Dialog.IsSecondaryButtonEnabled = context.Record is not null;
        }
    }

    private async Task OpenLogsFromRecoveryAsync(CrashRecord? record)
    {
        Acknowledge(record);
        try { await OpenCrashLogsAsync(); }
        catch (Exception exception)
        {
            this.Log().LogWarning(exception, "Could not open logs from recovery.");
        }
    }

    private async Task ShowEmergencyPanelIfNeededAsync(FrameworkElement root, CrashRecord? record)
    {
        // Escape or programmatic cancellation is not a user acknowledgement.
        // Keep recovery accessible inline instead of presenting another modal.
        if (!_normalStartupChosen) await ShowEmergencyRecoveryPanelAsync(root, record);
    }

    private static FrameworkElement CreateRecoveryDetails(CrashRecord? record)
        => new ScrollViewer
        {
            MaxHeight = 320,
            Content = new TextBlock
            {
                Text = record is null ? "UnexpectedShutdownDescription".Localize() : CrashReportFormatter.ToText(record),
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true
            }
        };

    private async Task ShowEmergencyRecoveryPanelAsync(FrameworkElement root, CrashRecord? record)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var panel = CreateEmergencyRecoveryPanel(record, completion);
        if (root is Frame frame)
        {
            frame.Content = panel;
        }
        await completion.Task;
    }

    private StackPanel CreateEmergencyRecoveryPanel(
        CrashRecord? record,
        TaskCompletionSource completion)
    {
        var panel = new StackPanel { Spacing = 12, Padding = new(24) };
        panel.Children.Add(new TextBlock
        {
            Text = record is null ? "RecoveryModeDescription".Localize() : "CrashRecoveryDescription".Localize(),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var details = CreateRecoveryDetails(record);
        details.Visibility = Visibility.Collapsed;
        var viewButton = new Button { Content = "ViewCrashReports".Localize() };
        viewButton.Click += (_, _) =>
        {
            Acknowledge(record);
            details.Visibility = Visibility.Visible;
        };
        panel.Children.Add(viewButton);
        panel.Children.Add(details);

        AddEmergencyReportActions(panel, record);
        AddEmergencyContinueAction(panel, record, completion);
        return panel;
    }

    private void AddEmergencyReportActions(StackPanel panel, CrashRecord? record)
    {
        if (record is null) return;

        var reportButton = new Button { Content = "ReportToGitHub".Localize() };
        reportButton.Click += async (_, _) =>
            await ReportFromEmergencyPanelAsync(record);
        panel.Children.Add(reportButton);

        var openLogsButton = new Button { Content = "OpenCrashLogs".Localize() };
        openLogsButton.Click += async (_, _) =>
            await OpenLogsFromEmergencyPanelAsync(record);
        panel.Children.Add(openLogsButton);
    }

    private async Task ReportFromEmergencyPanelAsync(CrashRecord record)
    {
        try
        {
            Acknowledge(record);
            await ReportCrashOnGitHubAsync(record);
        }
        catch (Exception exception)
        {
            this.Log().LogWarning(exception, "Could not report the previous crash from the recovery panel.");
        }
    }

    private async Task OpenLogsFromEmergencyPanelAsync(CrashRecord record)
    {
        try
        {
            Acknowledge(record);
            await OpenCrashLogsAsync();
        }
        catch (Exception exception)
        {
            this.Log().LogWarning(exception, "Could not open logs from the recovery panel.");
        }
    }

    private void AddEmergencyContinueAction(
        StackPanel panel,
        CrashRecord? record,
        TaskCompletionSource completion)
    {
        var continueButton = new Button
        {
            Content = (_crashCoordinator.IsRecoveryMode ? "TryNormalStartup" : "Continue").Localize()
        };
        continueButton.Click += (_, _) =>
        {
            Acknowledge(record);
            _normalStartupChosen = true;
            completion.TrySetResult();
        };
        panel.Children.Add(continueButton);
    }

    private async Task ReportCrashOnGitHubAsync(CrashRecord record)
    {
        var draft = new GitHubCrashIssueComposer("https://github.com/RiversideValley/Emerald").Compose(record);
        try
        {
            var package = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            package.SetText(draft.FullReport);
            Clipboard.SetContent(package);
        }
        catch (Exception exception)
        {
            this.Log().LogWarning(exception, "Could not copy the crash report to the clipboard.");
        }

        if (!Uri.TryCreate(draft.Url, UriKind.Absolute, out var uri)
            || !await Launcher.LaunchUriAsync(uri))
        {
            throw new InvalidOperationException("Could not open the GitHub crash draft.");
        }
    }

    private Task OpenCrashLogsAsync()
    {
        var logsPath = Path.GetDirectoryName(_crashCoordinator.ApplicationLogPath);
        if (!PlatformFolderLauncher.TryOpen(logsPath))
        {
            this.Log().LogWarning("Could not open Emerald application logs at {LogsPath}.", logsPath);
        }

        return Task.CompletedTask;
    }

    private sealed class RecoveryDialogContext(
        ContentDialog dialog,
        Button openLogsButton,
        FrameworkElement details,
        TextBlock status,
        CrashRecord? record)
    {
        public ContentDialog Dialog { get; } = dialog;
        public Button OpenLogsButton { get; } = openLogsButton;
        public FrameworkElement Details { get; } = details;
        public TextBlock Status { get; } = status;
        public CrashRecord? Record { get; } = record;
    }

    #endregion
}
