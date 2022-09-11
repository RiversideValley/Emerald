using CmlLib.Core.Auth;
using Microsoft.Toolkit.Uwp.Helpers;
using Emerald.UWP.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core.Preview;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Emerald.UWP
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static ResourceDictionary AcrylicStyle { get; private set; } = new ResourceDictionary { Source = new Uri("ms-appx:///Resources/Acrylic.xaml") };
        public static ResourceDictionary MicaStyle { get; private set; } = new ResourceDictionary { Source = new Uri("ms-appx:///Resources/Mica.xaml") };
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }
        public static event EventHandler<AppServiceTriggerDetails> AppServiceConnected = delegate { };
        public static AppServiceConnection Connection { get; private set; }
        public bool Loaded = false;
        public BackgroundTaskDeferral AppServiceDeferral { get; private set; }
        public async Task Initialize()
        {
            if (SystemInformation.Instance.IsFirstRun)
            {
                ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"] = false;
                UIResourceHelper.SetResource(ResourceStyle.Acrylic);
            }
            else
            {
                bool IsInAppSettings = false;
                try
                {
                    IsInAppSettings = (bool)ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"];
                }
                catch
                {
                    ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"] = false;
                    ApplicationData.Current.RoamingSettings.Values["MCChangelogs"] = "";
                }
                if (IsInAppSettings == false)
                {
                    await SettingsManager.LoadSettings();
                }
                else
                {
                    try
                    {
                        Core.MainCore.Launcher.ChangeLogsHTMLBody = ApplicationData.Current.RoamingSettings.Values["MCChangelogs"] as string;
                    }
                    catch { }
                    SettingsManager.DeserializeSettings(ApplicationData.Current.RoamingSettings.Values["InAppSettings"] as string);
                }
            }
            if (UIResourceHelper.CurrentStyle == null)
            {
                UIResourceHelper.SetResource(ResourceStyle.Acrylic);
            }
            if (vars.autoLog && vars.Accounts != null)
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Last)
                    {
                        if (item.Type == Enums.AccountType.Offline)
                        {
                            if (item.UserName == null)
                            {
                                vars.session = null;
                            }
                            else
                            {
                                vars.session = MSession.GetOfflineSession(item.UserName);
                            }
                        }
                        else
                        {
                            if (item.UserName != null && item.AccessToken != null && item.UUID != null)
                            {
                                vars.session = new MSession(item.UserName, item.AccessToken, item.UUID);
                            }
                        }
                        vars.CurrentAccountCount = item.Count;
                    }
                }
            }
        }
        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
            await Initialize();
            Loaded = true;

            Frame rootFrame = Window.Current.Content as Frame;
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }
                rootFrame.RequestedTheme = (ElementTheme)vars.Theme;
                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += App_CloseRequested;
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }
        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details)
            {
                // only accept connections from callers in the same package
                if (details.CallerPackageFamilyName == Package.Current.Id.FamilyName)
                {
                    // connection established from the fulltrust process
                    AppServiceDeferral = args.TaskInstance.GetDeferral();
                    args.TaskInstance.Canceled += TaskInstance_Canceled; ;

                    Connection = details.AppServiceConnection;
                    AppServiceConnected.Invoke(this, args.TaskInstance.TriggerDetails as AppServiceTriggerDetails);
                }
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {

        }

        protected async override void OnActivated(IActivatedEventArgs args)
        {

            if (args.Kind == ActivationKind.CommandLineLaunch)
            {
                await Initialize();
                Loaded = true;

                await SettingsManager.LoadSettings();
                Frame rootFrame = Window.Current.Content as Frame;

                // Do not repeat app initialization when the Window already has content,  
                // just ensure that the window is active  
                if (rootFrame == null)
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    rootFrame = new Frame();

                    rootFrame.NavigationFailed += OnNavigationFailed;
                    // Place the frame in the current Window
                    Window.Current.Content = rootFrame;
                    rootFrame.Navigate(typeof(MainPage));
                    SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += App_CloseRequested;
                }
                Window.Current.Activate();
            }
        }
        private async void App_CloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            e.Handled = true;
            if (Loaded)
            {
                if ((bool)ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"] == false)
                {
                    await SettingsManager.SaveSettings();
                }
                else
                {
                    ApplicationData.Current.RoamingSettings.Values["InAppSettings"] = await SettingsManager.SerializeSettings();
                    ApplicationData.Current.RoamingSettings.Values["MCChangelogs"] = Core.MainCore.Launcher.ChangeLogsHTMLBody;
                }
            }
            if (!await ApplicationView.GetForCurrentView().TryConsolidateAsync())
            {
                Application.Current.Exit();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
