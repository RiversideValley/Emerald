using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Emerald.Views;

public sealed partial class PlaytimePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private bool? _wideLayout;
    private bool? _compactLayout;
    private bool _active;
    private bool _refreshQueued;
    public PlaytimePageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<PlaytimePageViewModel>();

    public PlaytimePage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) =>
        {
            if (ViewModel.HasActive)
            {
                QueueRefresh();
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Initialize(e.Parameter as PlaytimeNavigation);
        _active = true;
        ViewModel.DataChanged += Changed;
        ViewModel.Activate();
        _timer.Start();
    }

    private void Changed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(QueueRefresh);
    }

    private void QueueRefresh()
    {
        if (!_active || _refreshQueued)
        {
            return;
        }

        _refreshQueued = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _refreshQueued = false;
            if (_active)
            {
                ViewModel.Refresh();
            }
        });
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _timer.Stop();
        _active = false;
        ViewModel.Deactivate();
        ViewModel.DataChanged -= Changed;
        base.OnNavigatedFrom(e);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(HomePage));
        }
    }

    private void PreviousSession_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ChangeSessionPage(-1);
    }

    private void NextSession_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ChangeSessionPage(1);
    }

    private void PreviousRanking_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ChangeRankingPage(-1);
    }

    private void NextRanking_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ChangeRankingPage(1);
    }

    private void Heatmap_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(320, e.NewSize.Width);
        if (HeatmapContent.Width != width)
        {
            HeatmapContent.Width = width;
        }
    }

    private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 1000;
        var compact = e.NewSize.Width < 640;
        if (_wideLayout == wide && _compactLayout == compact)
        {
            return;
        }

        _wideLayout = wide;
        _compactLayout = compact;
        LayoutRoot.Padding = new Thickness(compact ? 16 : 24);

        foreach (var grid in new[] { PatternGrid, HistoryGrid })
        {
            Grid.SetRow(grid.Children[1] as FrameworkElement, wide ? 0 : 1);
            Grid.SetColumn(grid.Children[1] as FrameworkElement, wide ? 1 : 0);

            grid.ColumnDefinitions[1].Width =
                wide ? new GridLength(grid == HistoryGrid ? 2 : 1, GridUnitType.Star) : new GridLength(0);
        }

        Grid.SetRow(ScopePicker, compact ? 1 : 0);
        Grid.SetColumn(ScopePicker, compact ? 1 : 2);
        Grid.SetRow(RangePicker, compact ? 1 : 0);
        Grid.SetColumn(RangePicker, compact ? 2 : 3);
        KpiLayout.MaximumRowsOrColumns = compact ? 2 : 5;
    }
}
