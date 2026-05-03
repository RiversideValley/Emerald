using System;
using System.Diagnostics;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.Helpers;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Serilog.Sinks.File;
using Microsoft.UI.Dispatching;
using Uno.Extensions;
using Uno.Extensions.Hosting;
using Uno.Resizetizer;

namespace Emerald;

/// <summary>
/// Hosts the Uno application composition root, startup flow, and crash handling.
/// </summary>
public partial class App : Application
{
    private const string MicrosoftClientId = "dfeccda7-604a-4895-b409-9d35f1679b5d";
    private const string ElyByClientId = "emerald1";
    private const string ElyByClientSecret = "_hrxVlIoEWm1sqRlruFevD5v87mYW4EKPdmPWlraQoVP6kOXxJV9Y-qMrcm7Znk4";
    private const string ElyByRedirectUri = "http://127.0.0.1:58135/oauth/elyby/";
    private const string CurrentReleaseNotes = """
What's new
- Emerald now stores Windows app data in the app's local ApplicationData folder.
- Minecraft instances now use the Instances folder by default.
- Release notes now appear once after a fresh install or app update.

Notes
- Existing custom Minecraft paths and saved instances are left where they are.
- You can still change the Minecraft path from Settings.
""";

    private Services.SettingsService SS;

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        // Fires BEFORE any catch block — catches swallowed exceptions
        AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
        {
            // Only log exceptions from your own assemblies to avoid noise
            var ns = e.Exception.TargetSite?.DeclaringType?.Namespace ?? "";
            if (ns.StartsWith("Emerald") || ns.StartsWith("CmlLib"))
            {
                Debug.WriteLine($"[FIRST CHANCE] {e.Exception.GetType().Name}: {e.Exception.Message}");
                Debug.WriteLine($"[FIRST CHANCE STACK] {e.Exception.StackTrace}");
            }
        };

        this.UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    public Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    #region  Services

    private void ConfigureAuthServices(IServiceCollection services)
    {
        //Ely.By
        services.AddSingleton<CoreX.Services.Auth.ElyBy.ElyByOAuthOptions>(_ =>
            new CoreX.Services.Auth.ElyBy.ElyByOAuthOptions(
                ElyByClientId,
                ElyByClientSecret,
                ElyByRedirectUri));
        services.AddSingleton<CoreX.Services.Auth.ElyBy.IElyByAuthClient>(provider =>
            new CoreX.Services.Auth.ElyBy.ElyByAuthClient(
                provider.GetRequiredService<ILogger<CoreX.Services.Auth.ElyBy.ElyByAuthClient>>(),
                provider.GetRequiredService<CoreX.Services.Auth.ElyBy.ElyByOAuthOptions>()));
        services.AddSingleton<CoreX.Services.Auth.ElyBy.IElyByAccountStore, CoreX.Services.Auth.ElyBy.ElyByAccountStore>();
        services.AddSingleton<CoreX.Services.Auth.ElyBy.IElyByOAuthBrowser>(provider =>
        {
            var dispatcherQueue = MainWindow?.DispatcherQueue
                                  ?? DispatcherQueue.GetForCurrentThread()
                                  ?? throw new InvalidOperationException("A DispatcherQueue is required for Ely.by browser authentication.");

            return new Services.ElyByLoopbackOAuthBrowser(
                provider.GetRequiredService<ILogger<Services.ElyByLoopbackOAuthBrowser>>(),
                dispatcherQueue);
        });

        //authLib
        services.AddSingleton<CoreX.Services.Auth.Authlib.IAuthlibInjectorService>(provider =>
            new CoreX.Services.Auth.Authlib.AuthlibInjectorService(
                provider.GetRequiredService<ILogger<CoreX.Services.Auth.Authlib.AuthlibInjectorService>>(),
                Path.Combine(DirectResoucres.LocalDataPath, "authlib-injector")));

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
                Path.Combine(DirectResoucres.LocalDataPath, "accounts", "cml_accounts.json"),
                notificationService: provider.GetRequiredService<CoreX.Notifications.INotificationService>(),
                elyByAuthClient: provider.GetRequiredService<CoreX.Services.Auth.ElyBy.IElyByAuthClient>(),
                elyByAccountStore: provider.GetRequiredService<CoreX.Services.Auth.ElyBy.IElyByAccountStore>(),
                elyByOAuthBrowser: provider.GetRequiredService<CoreX.Services.Auth.ElyBy.IElyByOAuthBrowser>(),
                authlibInjectorService: provider.GetRequiredService<CoreX.Services.Auth.Authlib.IAuthlibInjectorService>());
        });
    }

    private void ConfigureCoreServices(IServiceCollection services)
    {
        services.AddSingleton<CoreX.Core>();

        services.AddSingleton<CoreX.Runtime.IGameRuntimeService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<GameRuntimeService>>();
            var notificationService = provider.GetRequiredService<CoreX.Notifications.INotificationService>();
            var accountService = provider.GetRequiredService<CoreX.Services.IAccountService>();
            var runtimeSettings = provider.GetRequiredService<CoreX.Runtime.IGameRuntimeSettings>();
            var dispatcherQueue = MainWindow?.DispatcherQueue
                                  ?? DispatcherQueue.GetForCurrentThread()
                                  ?? throw new InvalidOperationException("A DispatcherQueue is required for the game runtime service.");

            return new GameRuntimeService(logger, notificationService, accountService, runtimeSettings, dispatcherQueue);
        });

        //Mod Loaders
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Fabric>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Forge>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.LiteLoader>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Quilt>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Optifine>();

        services.AddTransient<CoreX.Installers.ModLoaderRouter>();
    }

    private void ConfigureStoreServices(IServiceCollection services)
    {
        //Stores
        services.AddTransient<ModStore>();
        services.AddTransient<PluginStore>();
        services.AddTransient<ResourcePackStore>();
        services.AddTransient<ShaderStore>();
        services.AddTransient<DataPackStore>();
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ModStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<PluginStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ResourcePackStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ShaderStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<DataPackStore>());
        services.AddTransient<IGameStoreContentService, GameStoreContentService>();
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
        services.AddTransient<ViewModels.GamesPageViewModel>();
        services.AddTransient<ViewModels.NotificationListViewModel>();
        services.AddSingleton<ViewModels.AccountsPageViewModel>();
        services.AddTransient<ViewModels.LogsPageViewModel>();
        services.AddTransient<ViewModels.ModrinthStorePageViewModel>();
    }
    
    #endregion
    
    /// <summary>
    /// Registers the maintained services and viewmodels used by the active Uno shell.
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
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
        var logPath = Path.Combine(DirectResoucres.LocalDataPath, "logs", "app_.log");

        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseSerilog(true, configureLogger: x => x
                    .MinimumLevel.Debug()
                    .WriteTo.File(logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}"))
                .ConfigureServices((context, services) => ConfigureServices(services))
            );

        MainWindow = builder.Window;
#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon("Assets/Icon.ico");

        Host = builder.Build();
        Ioc.Default.ConfigureServices(Host.Services);
        this.Log().LogInformation("Application host built successfully. LogPath: {LogPath}.", logPath);

        SS = Ioc.Default.GetService<Services.SettingsService>();

        //load settings,
        SS.LoadData();
        this.Log().LogInformation("Application settings loaded.");

        var ac = Ioc.Default.GetService<CoreX.Services.IAccountService>();
        _ = ac.InitializeAsync(MicrosoftClientId);
        this.Log().LogInformation("Account service initialization requested.");

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;
            this.Log().LogDebug("Created a new root navigation frame for the main window.");
        }

        // When the navigation stack isn't restored navigate to the first page,
        // configuring the new page by passing required information as a navigation
        // parameter
        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
            this.Log().LogInformation("Navigated to the main page.");
        }

        MainWindow.Activate();
        MainWindow.Closed += MainWindow_Closed;
        this.Log().LogInformation("Main window activated.");
        _ = ShowReleaseNotesAtStartupAsync();
        _ = CheckForUpdatesAtStartupAsync();
    }

    /// <summary>
    /// Persists settings when the main window closes.
    /// </summary>
    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        this.Log().LogInformation("Main window is closing. Persisting settings.");
        SS.SaveData();
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


            await Task.Yield();

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
        e.Handled = true;
        HandleCrash(e.Exception, "UI UnhandledException");
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        HandleCrash((Exception)e.ExceptionObject, "AppDomain UnhandledException");
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        HandleCrash(e.Exception, "Task UnobservedException");
    }

    /// <summary>
    /// Single entry point for all crashes. Writes file FIRST, then shows dialog.
    /// </summary>
    private void HandleCrash(Exception exception, string source)
    {
        Debug.WriteLine($"[CRASH] Handling crash from {source}: {exception.Message}");

        // 1. Write crash file immediately — before anything else that could fail
        var crashPath = WriteCrashFile(exception, source);

        // 2. Flush Serilog so buffered logs are persisted
        try { Log.CloseAndFlush(); } catch { }

        // 3. Show dialog (best effort — crash is already saved)
        ShowPlatformErrorDialog(
            $"An unexpected error occurred ({source}).\nCrash report saved to:\n{crashPath}",
            exception
        );
    }

    /// <summary>
    /// Writes the crash report to disk and records the fatal exception through the configured logger.
    /// </summary>
    private string WriteCrashFile(Exception exception, string source)
    {
        var crashPath = "unknown";
        try
        {
            crashPath = Path.Combine(
                DirectResoucres.LocalDataPath,
                "crashes",
                $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(crashPath)!);

            File.WriteAllText(crashPath, BuildCrashReport(exception, source));

            // Also log to Serilog
            this.Log().LogCritical(exception,
                "Unhandled exception ({Source}). Platform: {Platform}",
                source, DirectResoucres.Platform);
        }
        catch (Exception writeEx)
        {
            // Absolute last resort
            Debug.WriteLine($"[CRASH WRITE FAILED] {writeEx}");
            Debug.WriteLine($"[ORIGINAL CRASH] {exception}");
        }
        return crashPath;
    }

    /// <summary>
    /// Builds the plain-text crash report that is written alongside fatal errors.
    /// </summary>
    private static string BuildCrashReport(Exception ex, string source)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== CRASH REPORT ===");
        sb.AppendLine($"Time:     {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Platform: {DirectResoucres.Platform}");
        sb.AppendLine($"Source:   {source}");
        sb.AppendLine();
        AppendException(sb, ex, 0);
        return sb.ToString();
    }

    /// <summary>
    /// Appends an exception and its inner exceptions to the crash report text.
    /// </summary>
    private static void AppendException(System.Text.StringBuilder sb, Exception? ex, int depth)
    {
        if (ex is null) return;
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}--- {(depth == 0 ? "Exception" : "Inner Exception")} ---");
        sb.AppendLine($"{indent}Type:    {ex.GetType().FullName}");
        sb.AppendLine($"{indent}Message: {ex.Message}");
        sb.AppendLine($"{indent}Stack:   {ex.StackTrace}");

        // Recursively unwrap inner exceptions
        if (ex is AggregateException agg)
            foreach (var inner in agg.InnerExceptions)
                AppendException(sb, inner, depth + 1);
        else
            AppendException(sb, ex.InnerException, depth + 1);
    }

    private async void ShowPlatformErrorDialog(string message, Exception ex)
    {
        try
        {
            await MessageBox.Show("AppCrash".Localize(), message, Helpers.Enums.MessageBoxButtons.Ok);
        }
        catch (Exception dialogEx)
        {
            // Dialog itself failed — log both errors properly
            Debug.WriteLine($"[DIALOG FAILED] {dialogEx}");
            Debug.WriteLine($"[ORIGINAL ERROR] {ex}");
        }
        finally
        {
            // Always kill — crash file is already saved at this point
            Process.GetCurrentProcess().Kill();
        }
    }

    #endregion
}
