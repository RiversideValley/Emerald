using Emerald.WinUI.Helpers.AppInstancing;
using Emerald.WinUI.Helpers.Updater;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.Storage;

namespace Emerald.WinUI;

public partial class App : Application
{
    private readonly SingleInstanceDesktopApp _singleInstanceApp;
    public Core.Emerald Launcher { get; private set; } = new();
    public Updater Updater { get; private set; } = new();
    public ElementTheme ActualTheme => (MainWindow.Content as FrameworkElement).ActualTheme;
    public App()
    {
        InitializeComponent();
        _singleInstanceApp = new SingleInstanceDesktopApp("Riverside.Emerald");
        _singleInstanceApp.Launched += OnSingleInstanceLaunched;
    }

    /// <summary>
    /// Sets the settings data from <paramref name="settings"/> then restart the application
    /// </summary>
    public void LoadFromBackupSettings(string settings)
    {
        saveData = false;
        ApplicationData.Current.RoamingSettings.Values["Settings"] = settings;
        MainWindow.Close();
        var p = new Process()
        {
            StartInfo = new()
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = $"{Windows.ApplicationModel.Package.Current.InstalledPath}\\Restart.bat"
            }
        };
        p.Start();
    }

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public new static App Current => (App)Application.Current;

    // Redirect the OnLaunched event to the single app instance 
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstanceApp.Launch(args.Arguments);
    }
    bool saveData = true;
    private void InitializeMainWindow()
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
        MainWindow.Closed += (_, _) => { if (saveData) Helpers.Settings.SettingsSystem.SaveData(); };
    }
    private void OnSingleInstanceLaunched(object? sender, SingleInstanceLaunchEventArgs e)
    {
        if (e.IsFirstLaunch)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 256;
            _ = Updater.Initialize();
            InitializeMainWindow();
        }
        else
        {
            // TODO: do things on subsequent launches, like processing arguments from e.Arguments
        }
    }

    public Window MainWindow { get; private set; }
}