using System;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
using Emerald.Helpers;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Text;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Text;

namespace Emerald.Views;

public sealed partial class AccountsPage : Page
{
    public AccountsPageViewModel ViewModel { get; }
    public AccountsPage()
    {
        ViewModel = Ioc.Default.GetService<AccountsPageViewModel>();
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeCommand.ExecuteAsync(null);
    }

    private ContentDialog AddAccountDialog = new();
    private void BuilAndShowdProviderMethodsMenu()
    {
        var pnl = new StackPanel();

        foreach (var provider in ViewModel.Providers)
        {
            var txt = new TextBlock
            {
                Text = provider.DisplayName,
                FontWeight = FontWeights.SemiBold,
                Margin =  new Thickness(6, 6, 6,0),
            };
            pnl.Children.Add(txt);

            var usability = ViewModel.GetProviderUsability(provider.ProviderId);
            foreach (var method in provider.SignInMethods)
            {
                var item = new SettingsCard()
                {
                    Margin =  new Thickness(6, 4, 6, 0),
                    Header = method.DisplayName,
                    IsClickEnabled = true,
                    Description = usability.IsAvailable
                        ? method.Description
                        : $"{provider.DisplayName}: {usability.UnavailableReason}",
                    IsEnabled = usability.IsAvailable,
                    Tag = new ProviderSignInSelection(provider.ProviderId, method.MethodId, method.InputKind)
                };
                item.Click += ProviderMethod_Click;
                pnl.Children.Add(item);
            }
        }
        AddAccountDialog = pnl.ToContentDialog("AddAccount".Localize(), "Cancel".Localize());
        AddAccountDialog.ShowAsync();
    }

    private async void ProviderMethod_Click(object sender, RoutedEventArgs args)
    {
        AddAccountDialog.Hide();

        if ((sender as FrameworkElement)?.Tag is not ProviderSignInSelection selection)
            return;

        string? username = null;
        if (selection.InputKind == AccountSignInInputKind.Username)
        {
            var usernameInput = new TextBox
            {
                Header = "Username".Localize(),
                PlaceholderText = "EnterYourDesiredUsername".Localize()
            };
            var dialog = usernameInput.ToContentDialog(
                "AddOfflineAccount".Localize(),
                PrimaryButtonText: "Add".Localize(),
                closebtnText: "Cancel".Localize(),
                defaultButton: ContentDialogButton.Primary);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
            username = usernameInput.Text.Trim();
        }

        await ViewModel.AddProviderAccountAsync(selection.ProviderId, selection.MethodId, username);
    }

    private async void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not EAccount account) return;

        var confirmationDialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "RemoveAccount".Localize(),
            Content = string.Format("RemoveAccountConfirmation".Localize(), account.Name),
            PrimaryButtonText = "Remove".Localize(),
            CloseButtonText = "Cancel".Localize(),
            DefaultButton = ContentDialogButton.Close
        };

        var result = await confirmationDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.RemoveAccountCommand.ExecuteAsync(account);
        }
    }

    private async void AccountCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not EAccount account)
        {
            return;
        }

        await ViewModel.ActivateAccountCommand.ExecuteAsync(account);
    }

    private async void RefreshAccount_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is EAccount account)
            await ViewModel.RefreshAccountAsync(account);
    }

    private async void ProviderAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AccountProviderActionDescriptor action)
            await Launcher.LaunchUriAsync(action.Uri);
    }

    private sealed record ProviderSignInSelection(string ProviderId, string MethodId, AccountSignInInputKind InputKind);

    private void AddAccountButton_OnClick(object sender, RoutedEventArgs e)
    {
        BuilAndShowdProviderMethodsMenu();
    }
}
