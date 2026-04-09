using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;
using WinRT;
using WinRT.Interop;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Foundation;

namespace Emerald.Helpers;

/// <summary>
/// Provides shell-level window helpers for icon, title bar, and backdrop configuration.
/// </summary>
public static class WindowManager
{
    private static ILogger Logger
    {
        get
        {
            try
            {
                return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(WindowManager).FullName!)
                    ?? NullLogger.Instance;
            }
            catch (InvalidOperationException)
            {
                return NullLogger.Instance;
            }
        }
    }

    /// <summary>
    /// This will set the Window Icon for the given <see cref="global::Microsoft.UI.Xaml.Window" /> using the provided UnoIcon.
    /// </summary>
    public static void SetWindowIcon(this global::Microsoft.UI.Xaml.Window window, string iconpath = "icon.ico")
    {
#if WINDOWS && !HAS_UNO
            Logger.LogDebug("Setting window icon to {IconPath}.", iconpath);
            var hWnd = global::WinRT.Interop.WindowNative.GetWindowHandle(window);

            // Retrieve the WindowId that corresponds to hWnd.
            global::Microsoft.UI.WindowId windowId = global::Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);

            // Lastly, retrieve the AppWindow for the current (XAML) WinUI 3 window.
            global::Microsoft.UI.Windowing.AppWindow appWindow = global::Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(iconpath);

            // Set the Window Title Only if it has the Default WinUI Desktop value and we are running Unpackaged
            if (appWindow.Title == "WinUI Desktop")
            {
                appWindow.Title = "Emerald";
            }
#endif
    }
    /// <summary>
    /// Add mica and the icon to the <paramref name="window"/>
    /// </summary>
    public static MicaBackground? IntializeWindow(Window window)
    {
#if WINDOWS
        Logger.LogInformation("Initializing Mica backdrop for the main window.");
        var s = new MicaBackground(window);
            s.TrySetMicaBackdrop();
            return s;
#endif
        Logger.LogDebug("Skipping Mica backdrop initialization because the current platform does not support it.");
        return null;
    }

    /// <summary>
    /// Sets the customized titlebar if supported
    /// </summary>
    /// <exception cref="NullReferenceException"/>
    public static void SetTitleBar(Window window, UIElement AppTitleBar)
    {
            Logger.LogDebug("Applying custom title bar configuration.");
            var titleBar = window.AppWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

    }
}

/// <summary>
/// Ensures a Windows system dispatcher queue exists before system backdrop APIs are used.
/// </summary>
public class WindowsSystemDispatcherQueueHelper
{
    private object? _dispatcherQueueController;
    private static ILogger Logger
    {
        get
        {
            try
            {
                return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(WindowsSystemDispatcherQueueHelper).FullName!)
                    ?? NullLogger.Instance;
            }
            catch (InvalidOperationException)
            {
                return NullLogger.Instance;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object? dispatcherQueueController);

    public void EnsureWindowsSystemDispatcherQueueController()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
        {
            // one already exists, so we'll just use it.
            Logger.LogDebug("Windows system dispatcher queue already exists for the current thread.");
            return;
        }

        if (_dispatcherQueueController == null)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
            options.threadType = 2;
            options.apartmentType = 2;

            CreateDispatcherQueueController(options, ref _dispatcherQueueController);
            Logger.LogInformation("Created a Windows system dispatcher queue controller.");
        }
    }
}

/// <summary>
/// Wraps WinUI Mica backdrop configuration for the active application window.
/// </summary>
public class MicaBackground
{
#if WINDOWS
    private readonly Window _window;
    public readonly MicaController MicaController = new();
    private SystemBackdropConfiguration _backdropConfiguration = new();
    private readonly WindowsSystemDispatcherQueueHelper _dispatcherQueueHelper = new();
    private static ILogger Logger
    {
        get
        {
            try
            {
                return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(MicaBackground).FullName!)
                    ?? NullLogger.Instance;
            }
            catch (InvalidOperationException)
            {
                return NullLogger.Instance;
            }
        }
    }

    public MicaBackground(Window window)
    {
        _window = window;
    }

    public bool TrySetMicaBackdrop()
    {
        if (MicaController.IsSupported())
        {
            _dispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();
            _window.Activated += WindowOnActivated;
            _window.Closed += WindowOnClosed;
            ((FrameworkElement)_window.Content).ActualThemeChanged += MicaBackground_ActualThemeChanged;
            _backdropConfiguration.IsInputActive = true;

            _backdropConfiguration.Theme = _window.Content switch
            {
                FrameworkElement { ActualTheme: ElementTheme.Dark } => SystemBackdropTheme.Dark,
                FrameworkElement { ActualTheme: ElementTheme.Light } => SystemBackdropTheme.Light,
                FrameworkElement { ActualTheme: ElementTheme.Default } => SystemBackdropTheme.Default,
                _ => throw new InvalidOperationException("Unknown theme")
            };

            MicaController.AddSystemBackdropTarget(_window.As<ICompositionSupportsSystemBackdrop>());
            MicaController.SetSystemBackdropConfiguration(_backdropConfiguration);
            Logger.LogInformation("Applied Mica backdrop to the main window.");

            return true;
        }

        Logger.LogDebug("Mica backdrop is not supported on the current window.");
        return false;
    }

    private void MicaBackground_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_backdropConfiguration != null)
        {
            Logger.LogDebug("Updating Mica backdrop theme after theme change.");
            SetConfigurationSourceTheme();
        }
    }

    private void SetConfigurationSourceTheme()
    {
        switch (((FrameworkElement)_window.Content).ActualTheme)
        {
            case ElementTheme.Dark: _backdropConfiguration.Theme = SystemBackdropTheme.Dark; break;
            case ElementTheme.Light: _backdropConfiguration.Theme = SystemBackdropTheme.Light; break;
            case ElementTheme.Default: _backdropConfiguration.Theme = SystemBackdropTheme.Default; break;
        }
    }

    private void WindowOnClosed(object sender, WindowEventArgs args)
    {
        Logger.LogDebug("Disposing Mica backdrop resources after the window closed.");
        MicaController.Dispose();
        _window.Activated -= WindowOnActivated;
        _backdropConfiguration = null!;
    }

    private void WindowOnActivated(object sender, WindowActivatedEventArgs args)
    {
        _backdropConfiguration.IsInputActive = args.WindowActivationState is not WindowActivationState.Deactivated;
        Logger.LogDebug("Updated Mica backdrop activation state. IsInputActive: {IsInputActive}.", _backdropConfiguration.IsInputActive);
    }
#endif
}
