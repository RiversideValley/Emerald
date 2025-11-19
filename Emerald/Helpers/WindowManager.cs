using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
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

public static class WindowManager
{
    /// <summary>
    /// This will set the Window Icon for the given <see cref="global::Microsoft.UI.Xaml.Window" /> using the provided UnoIcon.
    /// </summary>
    public static void SetWindowIcon(this global::Microsoft.UI.Xaml.Window window, string iconpath = "icon.ico")
    {
#if WINDOWS && !HAS_UNO
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

        var s = new MicaBackground(window);
            s.TrySetMicaBackdrop();
            return s;
#endif
        return null;
    }

    /// <summary>
    /// Sets the customized titlebar if supported
    /// </summary>
    /// <exception cref="NullReferenceException"/>
    public static void SetTitleBar(Window window, UIElement AppTitleBar)
    {
        FrameworkElement RootUI = (FrameworkElement)window.Content;
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titlebar = window.AppWindow.TitleBar;
            titlebar.ExtendsContentIntoTitleBar = true;

            void SetColor(ElementTheme acualTheme)
            {
                titlebar.ButtonBackgroundColor = titlebar.ButtonInactiveBackgroundColor = titlebar.ButtonPressedBackgroundColor = Colors.Transparent;
                switch (acualTheme)
                {
                    case ElementTheme.Dark:
                        titlebar.ButtonHoverBackgroundColor = Colors.Black;
                        titlebar.ButtonForegroundColor = Colors.White;
                        titlebar.ButtonHoverForegroundColor = Colors.White;
                        titlebar.ButtonPressedForegroundColor = Colors.Silver;
                        break;
                    case ElementTheme.Light:
                        titlebar.ButtonHoverBackgroundColor = Colors.White;
                        titlebar.ButtonForegroundColor = Colors.Black;
                        titlebar.ButtonHoverForegroundColor = Colors.Black;
                        titlebar.ButtonPressedForegroundColor = Colors.DarkGray;
                        break;
                }
            }

            RootUI.ActualThemeChanged += (s, _) => SetColor(s.ActualTheme);
            window.SetTitleBar(AppTitleBar);
            SetColor(RootUI.ActualTheme);
        }
        else
        {
            window.ExtendsContentIntoTitleBar = true;
            window.SetTitleBar(AppTitleBar);
        }
    }
}

public class WindowsSystemDispatcherQueueHelper
{
    private object? _dispatcherQueueController;

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
            return;
        }

        if (_dispatcherQueueController == null)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
            options.threadType = 2;
            options.apartmentType = 2;

            CreateDispatcherQueueController(options, ref _dispatcherQueueController);
        }
    }
}

public class MicaBackground
{
#if WINDOWS
    private readonly Window _window;
    public readonly MicaController MicaController = new();
    private SystemBackdropConfiguration _backdropConfiguration = new();
    private readonly WindowsSystemDispatcherQueueHelper _dispatcherQueueHelper = new();

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

            return true;
        }

        return false;
    }

    private void MicaBackground_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_backdropConfiguration != null)
        {
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
        MicaController.Dispose();
        _window.Activated -= WindowOnActivated;
        _backdropConfiguration = null!;
    }

    private void WindowOnActivated(object sender, WindowActivatedEventArgs args)
    {
        _backdropConfiguration.IsInputActive = args.WindowActivationState is not WindowActivationState.Deactivated;
    }
#endif
}
