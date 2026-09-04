using System.Runtime.InteropServices;

namespace Emerald;

/// <summary>
/// Observes AppKit's normal termination notification without replacing Uno's
/// NSApplication delegate. Sudden termination does not send this notification,
/// so the lifecycle marker remains unclean in that case.
/// </summary>
internal static class MacApplicationTerminationObserver
{
    private const string NotificationName = "NSApplicationWillTerminateNotification";
    private static readonly object Gate = new();
    private static readonly NotificationCallback Callback = OnNotification;
    private static Action? _onTerminate;
    private static IntPtr _observer;

    public static void Register(Action onTerminate)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        lock (Gate)
        {
            _onTerminate = onTerminate;
            if (_observer != IntPtr.Zero)
            {
                return;
            }

            try
            {
                var observerClass = objc_getClass("EmeraldTerminationObserver");
                if (observerClass == IntPtr.Zero)
                {
                    observerClass = objc_allocateClassPair(objc_getClass("NSObject"), "EmeraldTerminationObserver", IntPtr.Zero);
                    if (observerClass == IntPtr.Zero)
                    {
                        return;
                    }

                    class_addMethod(
                        observerClass,
                        sel_registerName("emeraldWillTerminate:"),
                        Marshal.GetFunctionPointerForDelegate(Callback),
                        "v@:@");
                    objc_registerClassPair(observerClass);
                }

                _observer = objc_msgSend(observerClass, sel_registerName("new"));
                var center = objc_msgSend(objc_getClass("NSNotificationCenter"), sel_registerName("defaultCenter"));
                var notificationName = CreateNSString(NotificationName);
                objc_msgSend(
                    center,
                    sel_registerName("addObserver:selector:name:object:"),
                    _observer,
                    sel_registerName("emeraldWillTerminate:"),
                    notificationName,
                    IntPtr.Zero);
            }
            catch
            {
                _observer = IntPtr.Zero;
            }
        }
    }

    private static void OnNotification(IntPtr self, IntPtr selector, IntPtr notification)
    {
        try
        {
            _onTerminate?.Invoke();
        }
        catch
        {
            // Never allow an exception to cross the Objective-C callback boundary.
        }
    }

    private static IntPtr CreateNSString(string value)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return objc_msgSend(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotificationCallback(IntPtr self, IntPtr selector, IntPtr notification);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass, [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr extraBytes);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_addMethod(IntPtr cls, IntPtr selector, IntPtr implementation, [MarshalAs(UnmanagedType.LPStr)] string types);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl)]
    private static extern void objc_registerClassPair(IntPtr cls);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.A.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4);
}
