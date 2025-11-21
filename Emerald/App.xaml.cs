using CommonServiceLocator;
using Serilog;
using Serilog.Sinks.File;
using Uno.Resizetizer;
using Microsoft.Extensions.DependencyInjection;
using Emerald.CoreX.Store.Modrinth;
using System.Diagnostics;
using System;
using Emerald.Helpers;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;

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
        //Notifications
        services.AddSingleton<CoreX.Notifications.INotificationService, CoreX.Notifications.NotificationService>();

        //Mod Loaders
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Fabric>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Forge>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.LiteLoader>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Quilt>();
        services.AddTransient<CoreX.Installers.IModLoaderInstaller, CoreX.Installers.Optifine>();

        services.AddTransient<CoreX.Installers.ModLoaderRouter>();

        //Core
        services.AddSingleton<CoreX.Core>();
        //Accounts
        services.AddSingleton<CoreX.Services.IAccountService, CoreX.Services.AccountService>();

        //Notifications
        services.AddTransient<ViewModels.NotificationListViewModel>();

        //ViewModels
        services.AddTransient<ViewModels.GamesPageViewModel>();
        services.AddTransient<ViewModels.AccountsPageViewModel>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var logPath = Path.Combine(
            DirectResoucres.LocalDataPath,
            "logs",
            "app_.log");

        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif

                //Enable Logging
                .UseSerilog(true, configureLogger: x=> x
                            .MinimumLevel.Debug()
                            .WriteTo.File(logPath,
                                            rollingInterval: RollingInterval.Day,
                                            retainedFileCountLimit: 7,
                                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}"))

                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services);
                })
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

        ac.InitializeAsync("dfeccda7-604a-4895-b409-9d35f1679b5d"); // Public client ID

        // Do not repeat app initialization when the Window already has content,
        // just ensure that the window is active
        if (MainWindow.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new Frame();

            // Place the frame in the current Window
            MainWindow.Content = rootFrame;
        }

        if (rootFrame.Content == null)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }
        // Ensure the current window is active
        MainWindow.Activate();

        MainWindow.Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        //save settings,
        SS.SaveData();
    }

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public new static App Current => (App)Application.Current;

    #region UnhandledExceptions
    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        LogUnhandledException(e.Exception, "UI UnhandledException");
        ShowPlatformErrorDialog($"An unexpected error occurred. The application needs to be closed.\nSee crash details at {DirectResoucres.LocalDataPath} for more details");
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        LogUnhandledException((Exception)e.ExceptionObject, "AppDomain UnhandledException");
        ShowPlatformErrorDialog($"A critical error occurred. The application needs to be closed.\nSee crash details at {DirectResoucres.LocalDataPath} for more details");
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        LogUnhandledException(e.Exception, "Task UnobservedException");
        ShowPlatformErrorDialog($"A unobserved error occurred. The application needs to be closed.\nSee crash details at {DirectResoucres.LocalDataPath} for more details");
    }

    private void LogUnhandledException(Exception exception, string source)
    {
        try
        {
            this.Log().LogCritical(exception,
                "Unhandled exception ({Source}). Platform: {Platform}",
                source,
                DirectResoucres.Platform
            );

            // Save to crash file (platform-specific path)
            var crashPath = Path.Combine(
                DirectResoucres.LocalDataPath,
                "crashes",
                $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(crashPath));
            File.WriteAllText(crashPath, $"""
                Exception Details:
                Time: {DateTime.Now}
                Platform: {DirectResoucres.Platform}
                Type: {exception.GetType().FullName}
                Message: {exception.Message}
                Stack Trace: {exception.StackTrace}
                """);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to log unhandled exception: {ex.Message}");
        }
    }

    private async void ShowPlatformErrorDialog(string message)
    {
        try
        {
            await MessageBox.Show("AppCrash".Localize(), message, Helpers.Enums.MessageBoxButtons.Ok);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error: {message}");
        }
        Process.GetCurrentProcess().Kill();
    }
    #endregion
}
