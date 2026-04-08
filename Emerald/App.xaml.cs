using System;
using System.Diagnostics;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Store.Modrinth;
using Emerald.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.File;
using Microsoft.UI.Dispatching;
using Uno.Resizetizer;

namespace Emerald;

public partial class App : Application
{
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

    private void ConfigureServices(IServiceCollection services)
    {
        //Stores
        services.AddTransient(provider => new ModStore(typeof(ModStore).Log()));
        services.AddTransient(provider => new PluginStore(typeof(PluginStore).Log()));
        services.AddTransient(provider => new ResourcePackStore(typeof(ResourcePackStore).Log()));
        services.AddTransient(provider => new ResourcePackStore(typeof(ShaderStore).Log()));
        services.AddTransient(provider => new ModpackStore(typeof(ModpackStore).Log()));

        //Settings
        services.AddSingleton<Services.SettingsService>();
        services.AddSingleton<Services.IBaseSettingsService, Services.BaseSettingsService>();
        services.AddSingleton<CoreX.Runtime.IGameRuntimeSettings, Services.GameRuntimeSettingsAdapter>();

        //Notifications
        services.AddSingleton<CoreX.Notifications.INotificationService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<CoreX.Notifications.NotificationService>>();
            var inner = new CoreX.Notifications.NotificationService(logger);
            return new DispatchedNotificationService(inner, MainWindow.DispatcherQueue);
        });
        services.AddTransient<ViewModels.NotificationListViewModel>();

        //Mod Loaders
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Fabric>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Forge>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.LiteLoader>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Quilt>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Optifine>();

        services.AddTransient<CoreX.Installers.ModLoaderRouter>();

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

        //Core
        services.AddSingleton<CoreX.Core>();
        //Accounts
        services.AddSingleton<CoreX.Services.IAccountService, CoreX.Services.AccountService>();

        //ViewModels
        services.AddTransient<ViewModels.GamesPageViewModel>();
        services.AddTransient<ViewModels.AccountsPageViewModel>();
        services.AddTransient<ViewModels.LogsPageViewModel>();
    }

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

        SS = Ioc.Default.GetService<Services.SettingsService>();

        //load settings,
        SS.LoadData();

        var ac = Ioc.Default.GetService<CoreX.Services.IAccountService>();
        ac.InitializeAsync("dfeccda7-604a-4895-b409-9d35f1679b5d");
        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;
        }

        // When the navigation stack isn't restored navigate to the first page,
        // configuring the new page by passing required information as a navigation
        // parameter
        if (rootFrame.Content == null)
            rootFrame.Navigate(typeof(MainPage), args.Arguments);

        MainWindow.Activate();
        MainWindow.Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        SS.SaveData();
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
