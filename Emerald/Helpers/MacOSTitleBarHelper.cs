using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;

namespace Emerald.Helpers;

public static class MacOSTitleBarHelper
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    private const ulong NSWindowStyleMaskFullSizeContentView = 1UL << 15;
    private const long NSWindowTitleHidden = 1;

    private static readonly ILogger Logger = CreateLogger();

    private static readonly Lazy<MethodInfo?> GetNativeWindowMethod = new(ResolveGetNativeWindowMethod);
    private static readonly Lazy<PropertyInfo?> HandleProperty = new(() => typeof(IntPtr).GetProperty("Handle"));

    [DllImport(LibObjC, EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjC, EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_long(IntPtr receiver, IntPtr selector, long value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern long long_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern ulong ulong_objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_ulong(IntPtr receiver, IntPtr selector, ulong value);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend_long(IntPtr receiver, IntPtr selector, long index);

    public static void ExtendViewIntoTitleBar(Window window)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        try
        {
            var nsWindow = TryGetNSWindowHandle(window);
            if (nsWindow == IntPtr.Zero)
            {
                Logger.LogWarning("Could not obtain the native NSWindow handle. macOS titlebar customization skipped.");
                return;
            }

            ApplyTitleBarCustomization(nsWindow);
            Logger.LogInformation("Applied macOS transparent titlebar configuration via ObjC runtime interop.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply macOS titlebar customization.");
        }
    }

    private static void ApplyTitleBarCustomization(IntPtr nsWindow)
    {
        var selStyleMask = sel_registerName("styleMask");
        var selSetStyleMask = sel_registerName("setStyleMask:");
        var currentMask = ulong_objc_msgSend(nsWindow, selStyleMask);
        var newMask = currentMask | NSWindowStyleMaskFullSizeContentView;
        void_objc_msgSend_ulong(nsWindow, selSetStyleMask, newMask);

        var selSetTitlebarTransparent = sel_registerName("setTitlebarAppearsTransparent:");
        void_objc_msgSend_bool(nsWindow, selSetTitlebarTransparent, true);

        var selSetTitleVisibility = sel_registerName("setTitleVisibility:");
        void_objc_msgSend_long(nsWindow, selSetTitleVisibility, NSWindowTitleHidden);

        var selSetOpaque = sel_registerName("setOpaque:");
        void_objc_msgSend_bool(nsWindow, selSetOpaque, false);

        var nsColorClass = objc_getClass("NSColor");
        if (nsColorClass != IntPtr.Zero)
        {
            var selClearColor = sel_registerName("clearColor");
            var clearColor = IntPtr_objc_msgSend(nsColorClass, selClearColor);

            if (clearColor != IntPtr.Zero)
            {
                var selSetBackgroundColor = sel_registerName("setBackgroundColor:");
                void_objc_msgSend_IntPtr(nsWindow, selSetBackgroundColor, clearColor);
            }
        }
    }

    private static IntPtr TryGetNSWindowHandle(Window window)
    {
        var fromWindowHelper = TryGetNSWindowViaWindowHelper(window);
        if (fromWindowHelper != IntPtr.Zero)
            return fromWindowHelper;

        return TryGetNSWindowViaNSApplication();
    }

    private static IntPtr TryGetNSWindowViaWindowHelper(Window window)
    {
        try
        {
            var method = GetNativeWindowMethod.Value;
            if (method is null)
                return IntPtr.Zero;

            var nativeWindow = method.Invoke(null, new object[] { window });
            return ExtractNativeHandle(nativeWindow);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "WindowHelper strategy failed.");
            return IntPtr.Zero;
        }
    }

    private static MethodInfo? ResolveGetNativeWindowMethod()
    {
        var typeNames = new[]
        {
            "Uno.UI.Xaml.WindowHelper, Uno.UI",
            "Uno.UI.Xaml.WindowHelper, Uno.WinUI"
        };

        foreach (var typeName in typeNames)
        {
            var type = Type.GetType(typeName, throwOnError: false);
            var method = type?.GetMethod("GetNativeWindow", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
                return method;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType("Uno.UI.Xaml.WindowHelper", throwOnError: false);
            if (type == null)
                continue;

            var method = type.GetMethod("GetNativeWindow", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
                return method;
        }

        return null;
    }

    private static IntPtr TryGetNSWindowViaNSApplication()
    {
        try
        {
            var nsAppClass = objc_getClass("NSApplication");
            if (nsAppClass == IntPtr.Zero)
                return IntPtr.Zero;

            var sharedApp = IntPtr_objc_msgSend(nsAppClass, sel_registerName("sharedApplication"));
            if (sharedApp == IntPtr.Zero)
                return IntPtr.Zero;

            var mainWindow = IntPtr_objc_msgSend(sharedApp, sel_registerName("mainWindow"));
            if (mainWindow != IntPtr.Zero)
                return mainWindow;

            var keyWindow = IntPtr_objc_msgSend(sharedApp, sel_registerName("keyWindow"));
            if (keyWindow != IntPtr.Zero)
                return keyWindow;

            var windowsArray = IntPtr_objc_msgSend(sharedApp, sel_registerName("windows"));
            if (windowsArray == IntPtr.Zero)
                return IntPtr.Zero;

            var count = long_objc_msgSend(windowsArray, sel_registerName("count"));
            if (count <= 0)
                return IntPtr.Zero;

            return IntPtr_objc_msgSend_long(windowsArray, sel_registerName("objectAtIndex:"), 0);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "NSApplication fallback strategy failed.");
            return IntPtr.Zero;
        }
    }

    private static IntPtr ExtractNativeHandle(object? nativeWindow)
    {
        if (nativeWindow is null)
            return IntPtr.Zero;

        if (nativeWindow is IntPtr ptr)
            return ptr;

        if (nativeWindow is nint nPtr)
            return (IntPtr)nPtr;

        var type = nativeWindow.GetType();

        var handleProp = type.GetProperty("Handle", BindingFlags.Public | BindingFlags.Instance);
        if (handleProp?.GetValue(nativeWindow) is IntPtr handle && handle != IntPtr.Zero)
            return handle;

        if (handleProp?.GetValue(nativeWindow) is nint nh && nh != IntPtr.Zero)
            return (IntPtr)nh;

        var nativeHandleProp = type.GetProperty("NativeHandle", BindingFlags.Public | BindingFlags.Instance);
        if (nativeHandleProp?.GetValue(nativeWindow) is IntPtr nativeHandle && nativeHandle != IntPtr.Zero)
            return nativeHandle;

        return IntPtr.Zero;
    }

    private static ILogger CreateLogger()
    {
        try
        {
            return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(MacOSTitleBarHelper).FullName!)
                   ?? NullLogger.Instance;
        }
        catch
        {
            return NullLogger.Instance;
        }
    }
}
