using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Emerald.WinUI.UserControls
{

    /// <summary>
    /// A nice control from the RiseMP
    /// </summary>
    public sealed partial class AdaptiveItemPane : UserControl
    {

        public static DependencyProperty LeftPaneProperty =
            DependencyProperty.Register("LeftPane", typeof(object),
                typeof(AdaptiveItemPane), new PropertyMetadata(null));

        public object LeftPane
        {
            get => GetValue(LeftPaneProperty);
            set => SetValue(LeftPaneProperty, value);
        }
        private object RealLeft => GetValue(LeftPaneProperty) ?? GetValue(MiddlePaneProperty) ?? GetValue(RightPaneProperty);
        public static DependencyProperty RightPaneProperty =
            DependencyProperty.Register("RightPane", typeof(object),
                typeof(AdaptiveItemPane), new PropertyMetadata(null));
        private object RealRight => GetValue(MiddlePaneProperty) == null || GetValue(LeftPaneProperty) == null ? null : GetValue(RightPaneProperty);
        public object RightPane
        {
            get => GetValue(RightPaneProperty);
            set => SetValue(RightPaneProperty, value);
        }


        public static DependencyProperty MiddlePaneProperty =
            DependencyProperty.Register("MiddlePane", typeof(object),
                typeof(AdaptiveItemPane), new PropertyMetadata(null));
        private object RealMiddle => GetValue(MiddlePaneProperty) != null ? (GetValue(LeftPaneProperty) == null ? GetValue(RightPaneProperty) : GetValue(MiddlePaneProperty)) : (GetValue(LeftPaneProperty) == null ? null : GetValue(RightPaneProperty));
        public object MiddlePane
        {
            get => GetValue(MiddlePaneProperty);
            set => SetValue(MiddlePaneProperty, value);
        }


        public double MainBreakpoint { get; set; }
        public double LeftMiddleBreakpoint { get; set; }

        public static DependencyProperty StackCenterProperty =
            DependencyProperty.Register("StackCenter", typeof(bool),
                typeof(AdaptiveItemPane), new PropertyMetadata(false));

        public bool StackCenter
        {
            get => (bool)GetValue(StackCenterProperty);
            set => SetValue(StackCenterProperty, value);
        }

        public static DependencyProperty StretchContentProperty =
            DependencyProperty.Register("StretchContent", typeof(bool),
                typeof(AdaptiveItemPane), new PropertyMetadata(false));

        public bool StretchContent
        {
            get => (bool)GetValue(StretchContentProperty);
            set => SetValue(StretchContentProperty, value);
        }

        public static DependencyProperty OnlyStackedProperty =
            DependencyProperty.Register("OnlyStacked", typeof(bool),
                typeof(AdaptiveItemPane), new PropertyMetadata(false));

        public bool OnlyStacked
        {
            get => (bool)GetValue(OnlyStackedProperty);
            set => SetValue(OnlyStackedProperty, value);
        }
        private long _leftToken;
        private long _rightToken;

        public AdaptiveItemPane()
        {
            InitializeComponent();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            UpdateBreakpoint(this);
            PerformResize(ActualWidth);

            _leftToken = RegisterPropertyChangedCallback(LeftPaneProperty, OnPanesUpdated);
            _rightToken = RegisterPropertyChangedCallback(RightPaneProperty, OnPanesUpdated);
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            UnregisterPropertyChangedCallback(LeftPaneProperty, _leftToken);
            UnregisterPropertyChangedCallback(RightPaneProperty, _rightToken);
        }
    }

    // Event handlers
    public sealed partial class AdaptiveItemPane
    {
        public event EventHandler? OnStacked;
        public event EventHandler? OnStretched;
        public event EventHandler? OnMiddleState;
        private static void OnPanesUpdated(DependencyObject d, DependencyProperty dp)
        {
            if (d is AdaptiveItemPane pane)
                UpdateBreakpoint(pane);
        }

        private static void UpdateBreakpoint(AdaptiveItemPane pane)
        {
            pane.MainBreakpoint = pane.Left.ActualWidth + pane.Middle.ActualWidth + pane.Right.ActualWidth;
            pane.LeftMiddleBreakpoint = pane.Left.ActualWidth + pane.Middle.ActualWidth;
        }

        private void Pane_SizeChanged(object sender, SizeChangedEventArgs e)
            => PerformResize(e.NewSize.Width);
        public void Update() => PerformResize(this.ActualWidth);
        private void PerformResize(double width)
        {
            int margin = StretchContent ? (MiddlePane == null ? 12 : 24) : 0;
            bool NoRight = RealRight == null;
            if ((width - margin < MainBreakpoint && width - margin < LeftMiddleBreakpoint) || OnlyStacked)
            {
                VisualStateManager.GoToState(this, StackCenter ? "StackedCenter" : "Stacked", false);
                OnStacked?.Invoke(this, new());
            }
            else if (width - margin < MainBreakpoint)
            {
                VisualStateManager.GoToState(this, StretchContent ? "MiddleStateStretch" : "MiddleState", false);
                OnMiddleState?.Invoke(this, new());
            }
            else
            {
                VisualStateManager.GoToState(this, StretchContent ? (NoRight ? "SideBySideStretchNoRight" : "SideBySideStretch") : "SideBySide", false);
                OnStretched?.Invoke(this, new());
            }
        }
    }
}
