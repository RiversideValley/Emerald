using CommonServiceLocator;
using Serilog;
using Serilog.Sinks.File;
using Uno.Resizetizer;
using Uno.UI.HotDesign;
using Microsoft.Extensions.DependencyInjection;
using Emerald.CoreX.Store.Modrinth;

namespace Emerald; 
public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    public Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }
    public void ConfigureServices(IServiceCollection services)
    {

        services.AddTransient(provider => new ModStore(typeof(ModStore).Log()));
        services.AddTransient(provider => new PluginStore(typeof(PluginStore).Log()));
        services.AddTransient(provider => new ResourcePackStore(typeof(ResourcePackStore).Log()));
        services.AddTransient(provider => new ResourcePackStore(typeof(ShaderStore).Log()));
        services.AddTransient(provider => new ModpackStore(typeof(ModpackStore).Log()));
        
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Emerald",
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
                                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}.{Method}) {Message}{NewLine}{Exception}"))

                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services);
                })
            );

        MainWindow = builder.Window;
#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();

        //Help me.
        ServiceLocator.SetLocatorProvider(() => new Emerald.Helpers.Services.ServiceProviderLocator(Host!.Services));

        this.Log().LogInformation("New Instance was created. Logs are being saved at: {logPath}",logPath);

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
    }

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public new static App Current => (App)Application.Current;
}
