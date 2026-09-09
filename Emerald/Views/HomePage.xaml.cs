using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.Helpers;
using Emerald.Helpers.Enums;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Emerald.Views;

public sealed partial class HomePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public HomePageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<HomePageViewModel>();

    public HomePage()
    {
        InitializeComponent();
        var ticks = 0;
        _timer.Tick += (_, _) =>
        {
            ViewModel.RefreshRuntime();
            if (++ticks % 30 == 0)
            {
                ViewModel.RefreshAnalytics();
            }
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
        _timer.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _timer.Stop();
        base.OnNavigatedFrom(e);
    }

    private MainPage? Shell => App.Current?.MainWindow?.Content is Frame { Content: MainPage main } ? main : null;

    private void Accounts_Click(object sender, RoutedEventArgs e)
    {
        Shell?.NavigateToTag("Accounts");
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasAccount)
        {
            Shell?.NavigateToTag("Accounts");
            return;
        }

        if (ViewModel.SelectedGame?.CanLaunch != true && ViewModel.SelectedGame?.HasActiveSession != true)
        {
            Instances_Click(sender, e);
            return;
        }

        ViewModel.LaunchCommand.Execute(null);
    }

    private void Instances_Click(object sender, RoutedEventArgs e)
    {
        Shell?.NavigateToTag("Instances", ViewModel.SelectedGame);
    }

    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        Shell?.NavigateToTag("Logs", ViewModel.SelectedGame);
    }

    private void Servers_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ServersPage), ViewModel.SelectedGame);
    }

    private void Worlds_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(WorldsPage), ViewModel.SelectedGame);
    }

    private void PlaytimeCard_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(PlaytimePage),
            new PlaytimeNavigation(ViewModel.CurrentPlaytimeScope, PlaytimeRange.SevenDays));
    }

    private void MainMenu_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedDestination = MinecraftLaunchTargetKind.MainMenu;
    }

    private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 1000;
        var compact = e.NewSize.Width < 640;
        HeroGrid.ColumnDefinitions[0].Width = new GridLength(wide ? 2 : 1, GridUnitType.Star);
        HeroGrid.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(PlaytimeCard, wide ? 1 : 0);
        Grid.SetRow(PlaytimeCard, wide ? 0 : 1);
        LayoutRoot.Padding = new Thickness(compact ? 16 : 24);
        for (var i = 0; i < ShortcutGrid.Children.Count; i++)
        {
            Grid.SetColumn(ShortcutGrid.Children[i] as FrameworkElement, compact ? 0 : i);
            Grid.SetRow(ShortcutGrid.Children[i] as FrameworkElement, compact ? i : 0);
        }

        ShortcutGrid.ColumnDefinitions[1].Width = ShortcutGrid.ColumnDefinitions[2].Width =
            compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
    }

    private async void ProfileSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: QuickProfile profile })
        {
            await ViewModel.SelectProfileAsync(profile, false);
        }
    }

    private async void ProfilePlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: QuickProfile profile })
        {
            await ViewModel.SelectProfileAsync(profile, true);
        }
    }

    private async void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        await ShowProfileEditorAsync(null);
    }

    private void ProfileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: QuickProfile profile } anchor)
        {
            return;
        }

        var flyout = new MenuFlyout();
        Add(DashboardText.Get("Edit"), async () => await ShowProfileEditorAsync(profile));
        Add(DashboardText.Get("Duplicate"), () =>
        {
            Ioc.Default.GetRequiredService<IQuickProfileService>().Duplicate(profile.Id);
            ViewModel.ReloadProfiles();
        });
        Add(DashboardText.Get("MoveEarlier"), () =>
        {
            Ioc.Default.GetRequiredService<IQuickProfileService>().Move(profile.Id, -1);
            ViewModel.ReloadProfiles();
        });
        Add(DashboardText.Get("MoveLater"), () =>
        {
            Ioc.Default.GetRequiredService<IQuickProfileService>().Move(profile.Id, 1);
            ViewModel.ReloadProfiles();
        });
        Add(DashboardText.Get("Delete"), async () =>
        {
            var result = await MessageBox.Show(
                DashboardText.Get("DeleteProfile"),
                profile.Name, MessageBoxButtons.Custom,
                DashboardText.Get("Delete"),
                DashboardText.Get("Cancel"));

            if (result == MessageBoxResults.CustomResult1)
            {
                Ioc.Default.GetRequiredService<IQuickProfileService>().Remove(profile.Id);
                ViewModel.ReloadProfiles();
            }
        });
        flyout.ShowAt(anchor);

        void Add(string text, Action action)
        {
            var profiles = ViewModel.QuickProfiles.Select(x => x.Profile.Id).ToList();
            var index = profiles.IndexOf(profile.Id);
            var item = new MenuFlyoutItem
            {
                Text = text,
                IsEnabled = text == DashboardText.Get("MoveEarlier") ? index > 0 :
                    text == DashboardText.Get("MoveLater") ? index < profiles.Count - 1 : true
            };
            item.Click += (_, _) => action();
            flyout.Items.Add(item);
        }
    }

    private async Task ShowProfileEditorAsync(QuickProfile? existing)
    {
        var root = XamlRoot;
        if (root == null)
        {
            return;
        }

        var draft = new QuickProfileEditorViewModel(ViewModel,
            Ioc.Default.GetRequiredService<IAccountService>(), Ioc.Default.GetRequiredService<IQuickProfileService>(),
            Ioc.Default.GetRequiredService<CoreX.Services.Worlds.IMinecraftWorldService>(), existing);
        var editor = new Controls.QuickProfileEditor(draft);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = DashboardText.Get(existing == null ? "NewProfile" : "EditProfile"),
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Content = editor,
            PrimaryButtonText = DashboardText.Get("Save"),
            CloseButtonText = DashboardText.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = draft.CanSave
        };
        draft.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(draft.CanSave))
            {
                dialog.IsPrimaryButtonEnabled = draft.CanSave;
            }
        };
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && draft.CanSave)
            {
                Ioc.Default.GetRequiredService<IQuickProfileService>().Save(draft.CreateProfile());
                ViewModel.ReloadProfiles();
            }
        }
        finally
        {
            draft.CancelLoading();
        }
    }
}
