using System;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.Helpers;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Emerald.Views;

public sealed partial class AccountsPage : Page
{
    public AccountsPageViewModel ViewModel { get; }
    private TextBox OfflineUserNameTextBox = null!;
    private TextBox ElyByLoginTextBox = null!;
    private PasswordBox ElyByPasswordBox = null!;
    private TextBox ElyByTwoFactorTextBox = null!;

    public AccountsPage()
    {
        ViewModel = Ioc.Default.GetService<AccountsPageViewModel>();
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        OfflineUserNameTextBox = new TextBox()
        {
            Header = "Username".Localize(),
            PlaceholderText = "EnterYourDesiredUsername".Localize()
        };

        await ViewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void AddOfflineAccount_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanCreateOfflineAccount)
        {
            return;
        }

        // Clear previous username before showing
        OfflineUserNameTextBox.Text = string.Empty;
        var dia = OfflineUserNameTextBox.ToContentDialog("AddOfflineAccount".Localize(),PrimaryButtonText: "Add".Localize(), closebtnText: "Cancel".Localize(),defaultButton: ContentDialogButton.Primary);
       
        dia.PrimaryButtonClick += AddOfflineAccountDialog_PrimaryButtonClick;

        await dia.ShowAsync();

        dia.PrimaryButtonClick -= AddOfflineAccountDialog_PrimaryButtonClick;
    }

    private async void AddElyByAccount_Click(object sender, RoutedEventArgs e)
    {
        ElyByLoginTextBox = new TextBox
        {
            Header = "ElyByUsernameOrEmail".Localize(),
            PlaceholderText = "ElyByUsernameOrEmailPlaceholder".Localize()
        };

        ElyByPasswordBox = new PasswordBox
        {
            Header = "Password".Localize(),
            PlaceholderText = "ElyByPasswordPlaceholder".Localize()
        };

        ElyByTwoFactorTextBox = new TextBox
        {
            Header = "ElyByTwoFactorCode".Localize(),
            PlaceholderText = "ElyByTwoFactorCodePlaceholder".Localize()
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                ElyByLoginTextBox,
                ElyByPasswordBox,
                ElyByTwoFactorTextBox
            }
        };

        var dialog = content.ToContentDialog(
            "SignInWithElyBy".Localize(),
            PrimaryButtonText: "Login".Localize(),
            closebtnText: "Cancel".Localize(),
            defaultButton: ContentDialogButton.Primary);

        dialog.PrimaryButtonClick += AddElyByAccountDialog_PrimaryButtonClick;

        await dialog.ShowAsync();

        dialog.PrimaryButtonClick -= AddElyByAccountDialog_PrimaryButtonClick;
    }

    private void AddOfflineAccountDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.OfflineUsername = OfflineUserNameTextBox.Text.Trim();
        ViewModel.AddOfflineAccountCommand.Execute(null);
    }

    private void AddElyByAccountDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.ElyByLogin = ElyByLoginTextBox.Text.Trim();
        ViewModel.ElyByPassword = ElyByPasswordBox.Password;
        ViewModel.ElyByTwoFactorCode = ElyByTwoFactorTextBox.Text.Trim();
        ViewModel.AddElyByAccountCommand.Execute(null);
    }

    private async void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not EAccount account) return;

        var confirmationDialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Remove Account",
            Content = $"Are you sure you want to remove the account '{account.Name}'? This action cannot be undone.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
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
}
