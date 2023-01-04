// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

        public static DependencyProperty IsStackedCenterProperty =
            DependencyProperty.Register("IsStackedCenter", typeof(bool),
                typeof(AdaptiveItemPane), new PropertyMetadata(false));

        public bool IsStackedCenter
        {
            get => (bool)GetValue(IsStackedCenterProperty);
            set => SetValue(IsStackedCenterProperty, value);
        }
        
        public static DependencyProperty StretchContentProperty =
            DependencyProperty.Register("StretchContent", typeof(bool),
                typeof(AdaptiveItemPane), new PropertyMetadata(false));

        public bool StretchContent
        {
            get => (bool)GetValue(StretchContentProperty);
            set => SetValue(StretchContentProperty, value);
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

        private void PerformResize(double width)
        {
            int margin = StretchContent ? (MiddlePane == null ? 12 : 24) : 0;
            bool NoRight = RealRight == null;
            if (width - margin < MainBreakpoint && width - margin < LeftMiddleBreakpoint)
                VisualStateManager.GoToState(this, IsStackedCenter ? "StackedCenter" : "Stacked", false);
            else if (width - margin < MainBreakpoint)
                VisualStateManager.GoToState(this, StretchContent ? "MiddleStateStretch" : "MiddleState", false);
            else
                VisualStateManager.GoToState(this, StretchContent ? (NoRight ? "SideBySideStretchNoRight" : "SideBySideStretch") : "SideBySide", false);
        }
    }
}
