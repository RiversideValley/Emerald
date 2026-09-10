using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace Emerald.Helpers;

/// <summary>Native motion shared by the manual Frame navigation flows.</summary>
public static class AppMotion
{
    public const string PlaytimeForward = "Emerald.Playtime.Forward";
    public const string PlaytimeBack = "Emerald.Playtime.Back";
    public const string DestinationBack = "Emerald.Destination.Back";

    // Read at the point of use so a changed accessibility preference takes effect.
    public static bool Enabled => new UISettings().AnimationsEnabled;

    public static NavigationTransitionInfo Entrance() => Enabled
        ? new EntranceNavigationTransitionInfo()
        : new SuppressNavigationTransitionInfo();

    public static NavigationTransitionInfo DrillIn() => Enabled
        ? new DrillInNavigationTransitionInfo()
        : new SuppressNavigationTransitionInfo();

    public static void NavigateConnected(Frame frame, Type page, object? parameter, string key,
        FrameworkElement source)
    {
        var prepared = Prepare(key, source, false);
        try
        {
            if (!frame.Navigate(page, parameter, prepared ? new SuppressNavigationTransitionInfo() : DrillIn()))
            {
                Cancel(key);
            }
        }
        catch
        {
            Cancel(key);
            throw;
        }
    }

    public static void Back(Frame frame, Type fallback, string? key = null, FrameworkElement? source = null)
    {
        // Only connect to the intended destination, never to an unrelated back-stack entry.
        var connectsToHome = !frame.CanGoBack || frame.BackStack.Last().SourcePageType == fallback;
        var prepared = connectsToHome && key != null && source != null && Prepare(key, source, true);
        var transition = prepared ? new SuppressNavigationTransitionInfo() : DrillIn();
        try
        {
            if (frame.CanGoBack)
            {
                frame.GoBack(transition);
            }
            else if (!frame.Navigate(fallback, null, transition) && key != null)
            {
                Cancel(key);
            }
        }
        catch
        {
            if (key != null) Cancel(key);
            throw;
        }
    }

    private static bool Prepare(string key, FrameworkElement source, bool returning)
    {
#if WINDOWS
        if (!Enabled || !IsVisible(source)) return false;
        var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(key, source);
        if (returning) animation.Configuration = new DirectConnectedAnimationConfiguration();
        return true;
#else
        // Uno 6.6 exposes these APIs as stubs. Keep its supported page transition.
        return false;
#endif
    }

    // Call from Loaded, after bindings and layout, without awaiting network/data refreshes.
    public static void Start(string key, FrameworkElement destination)
    {
#if WINDOWS
        var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(key);
        if (animation == null) return;
        if (!Enabled || !IsVisible(destination) || !animation.TryStart(destination)) animation.Cancel();
#endif
    }

    public static void Cancel(string key)
    {
#if WINDOWS
        ConnectedAnimationService.GetForCurrentView().GetAnimation(key)?.Cancel();
#endif
    }

#if WINDOWS
    private static bool IsVisible(FrameworkElement element)
    {
        if (!element.IsLoaded || element.Visibility != Visibility.Visible || element.XamlRoot == null ||
            element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
        var bounds = element.TransformToVisual(null).TransformBounds(
            new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
        var size = element.XamlRoot.Size;
        return bounds.Right > 0 && bounds.Bottom > 0 && bounds.Left < size.Width && bounds.Top < size.Height;
    }
#endif

    public static TransitionCollection ItemTransitions()
    {
        var transitions = new TransitionCollection();
        if (!Enabled) return transitions;
#if WINDOWS
        transitions.Add(new AddDeleteThemeTransition());
        transitions.Add(new ReorderThemeTransition());
#endif
        transitions.Add(new RepositionThemeTransition { IsStaggeringEnabled = false });
        return transitions;
    }
}
