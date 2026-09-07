using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Emerald.Views.Settings;

public sealed partial class AdvancedPage : Page
{
    public AdvancedSettingsPageViewModel ViewModel { get; }

    public AdvancedPage()
    {
        ViewModel = Ioc.Default.GetRequiredService<AdvancedSettingsPageViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.InitializeAsync();
    }
}
