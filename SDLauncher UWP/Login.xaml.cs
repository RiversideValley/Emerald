using CmlLib.Core.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using CmlLib.Core.Auth.Microsoft.MsalClient;
using Microsoft.Identity.Client;
using Windows.UI.Xaml.Navigation;
using MojangAPI.Model;
using System.Net.Http;
using MojangAPI;
using Windows.ApplicationModel.DataTransfer;
using System.Threading.Tasks;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP
{
    public sealed partial class Login : ContentDialog
    {
        public Login()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
        private void UI(bool value)
        {
            this.IsEnabled = value;
        }

        private async void btnMSLogin_Click(object sender, RoutedEventArgs e)
        {
            var tempsession = vars.session;
            var x = new MSLogin();
            UI(false);
            btnMSLogin.Content = "Please wait";
            await x.Initialize();
            var result = await x.Login();
            if (result == MSLogin.Exceptions.Cancelled)
            {
                this.Hide();
                await new MessageBoxEx("Error", "User canceld the login!", MessageBoxEx.Buttons.Ok).ShowAsync();
                vars.session = tempsession;
                vars.UserName = tempsession.Username;
                this.ShowAsync();
            }
            else if (result == MSLogin.Exceptions.NoAccount)
            {
                this.Hide();
                await new MessageBoxEx("Error", "You don't have an Minecraft profile on that Microsoft account!", MessageBoxEx.Buttons.Ok).ShowAsync();
                vars.session = tempsession;
                vars.UserName = tempsession.Username;
                this.ShowAsync();
            }
            else if (result == MSLogin.Exceptions.ConnectFailed)
            {
                this.Hide();
                await new MessageBoxEx("Error", "Connection to login service failed!", MessageBoxEx.Buttons.Ok).ShowAsync();
                if (tempsession != null)
                {
                    vars.session = tempsession;
                    vars.UserName = tempsession.Username;
                }
                this.ShowAsync();
                UpdateAccounts();
            }
            else if (result == MSLogin.Exceptions.Success)
            {
                vars.UserName = vars.session.Username;
                this.Hide();
            }
            UI(true);
            btnMSLogin.Content = "Click here to login";
        }

        private void btnMSLogout_Click(object sender, RoutedEventArgs e)
        {

        }
        private void UpdateSession(MSession session)
        {
            vars.session = session;
            vars.UserName = session.Username.ToString();
            this.Hide();
        }

        private async void btnOfflineLog_Click(object sender, RoutedEventArgs e)
        {
            char[] delimiters = new char[] { ' ', '\r', '\n' };
            if (!string.IsNullOrEmpty(txtbxOffUsername.Text))
            {
                if (txtbxOffUsername.Text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length == 1)
                {
                    txtOffSats.Visibility = Visibility.Collapsed;
                    bool goAhead = true;
                    int count = 0;
                    foreach (var account in vars.Accounts)
                    {
                        if (account.Type == "Offline")
                        {
                            if(account.UserName == txtbxOffUsername.Text.Replace(" ", "").ToString())
                            {
                                count++;
                            }
                        }
                    }
                    if (count > 0)
                    {
                        string msg = "You already have " + count + " offline account(s) called \"" + txtbxOffUsername.Text.Replace(" ", "").ToString() + "\" \nAre you really want to add a new one";
                        this.Hide();
                        var m = new MessageBoxEx("Infomation", msg, MessageBoxEx.Buttons.YesNo);
                        await m.ShowAsync();
                        this.ShowAsync();
                        if (m.Result == MessageBoxEx.Results.No)
                        {
                            goAhead = false;
                        }
                        else
                        {
                            goAhead = true;
                        }
                    }
                    if (goAhead)
                    AddAccount(MSession.GetOfflineSession(txtbxOffUsername.Text.Replace(" ", "").ToString()));
                    
                }
                else
                {
                    txtOffSats.Text = "Too many names!";
                    //txtOffSats.Text = "You have to remove " + (txtbxOffUsername.Text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length - 1).ToString() + " words of your user name";
                    txtOffSats.Visibility = Visibility.Visible;
                }
            }
            else
            {
                txtOffSats.Text = "Enter a username! ";
                txtOffSats.Visibility = Visibility.Visible;
            }
        }

        private void txtbxOffUsername_KeyDown(object sender, KeyRoutedEventArgs e)
        {

        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            btnBack_Click(null, null);
            if (vars.theme != null)
            {
                if (Window.Current.Content is FrameworkElement fe)
                {
                    this.RequestedTheme = (ElementTheme)vars.theme;
                    fe.RequestedTheme = (ElementTheme)vars.theme;
                }
            }
            UpdateAccounts();
            if(vars.Accounts.Count > 1)
            {
                btnChooseAcc_Click(null, null);
            }
        }

        private void LoginFromCache(object sender, RoutedEventArgs e)
        {
            foreach (var item in vars.Accounts)
            {
                if (item.Last)
                {
                    item.Last = false;
                }
            }
            if (sender is Button btn)
            {
                foreach (var item in vars.Accounts)
                {
                    if(item.Count == int.Parse(btn.Tag.ToString()))
                    {
                        item.Last = true;
                        if(item.Type == "Offline")
                        {
                            UpdateSession(MSession.GetOfflineSession(item.UserName));
                        }
                        else
                        {
                            UpdateSession(new MSession(item.UserName, item.AccessToken, item.UUID));
                        }
                        vars.CurrentAccountCount = item.Count;
                    }
                }
            }
        }
        private void LogOutFromCache(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem itm)
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Count == int.Parse(itm.Tag.ToString()))
                    {
                        if (item.Type == "Offline")
                        {
                            if(item.Count == vars.CurrentAccountCount)
                            {
                                vars.session = null;
                                vars.UserName = "";
                                vars.CurrentAccountCount = null;
                            }
                        }
                        else
                        {
                            if (item.Count == vars.CurrentAccountCount)
                            {
                                vars.session = null;
                                vars.UserName = "";
                                vars.CurrentAccountCount = null;
                            }
                        }
                        vars.Accounts.Remove(item);
                        UpdateAccounts();
                        return;
                    }
                }
            }
        }


        void AddAccount(MSession session)
        {
            foreach (var item in vars.Accounts)
            {
                if (item.Last)
                {
                    item.Last = false;
                }
            }
            if (session.UserType == "Mojang")
            {
                vars.Accounts.Add(new Account(session.Username, "Offline", "null", "null", vars.AccountsCount + 1, true));
                vars.AccountsCount++;
            }
            else
            {
                vars.Accounts.Add(new Account(session.Username, session.UserType, session.AccessToken, session.UUID, vars.AccountsCount + 1, true));
                vars.AccountsCount++;
            }
            vars.session = session;
            vars.UserName = session.Username;
            vars.CurrentAccountCount = vars.AccountsCount;
            this.Hide();
        }
        void UpdateAccounts()
        {

            accountsRepeater.ItemsSource = null;
            accountsRepeater.ItemsSource = vars.Accounts;
            if (vars.Accounts.Count == 0)
            {
                txtEmpty.Visibility = Visibility.Visible;
            }
            else
            {
                txtEmpty.Visibility = Visibility.Collapsed;
            }
            accountsRepeater.UpdateLayout();
        }
        private void btnChooseAcc_Click(object sender, RoutedEventArgs e)
        {
            gridChoose.Visibility = Visibility.Visible;
            gridNew.Visibility = Visibility.Collapsed;
            gridSettingsOnline.Visibility = Visibility.Collapsed;
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            gridNew.Visibility = Visibility.Visible;
            gridChoose.Visibility = Visibility.Collapsed;
            gridSettingsOnline.Visibility = Visibility.Collapsed;
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem itm)
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Count == int.Parse(itm.Tag.ToString()))
                    {
                        if (item.Type == "Offline")
                        {
                            itmRename.IsEnabled = true;
                            txtTypeSettings.Text = "Offline Account";
                            fnticoAcTypeSettings.Glyph = item.TypeIconGlyph;
                            prpSettings.DisplayName = item.UserName;
                            txtSettingsPrpName.Text = item.UserName;
                            chkItmAutoLog.Tag = item.Count.ToString();
                            itmRemove.Tag = item.Count.ToString();
                            btnCopyUUID.Tag = item.Count.ToString();
                            btnCopyToken.Tag = item.Count.ToString();
                            txtbxRename.Tag = item.Count.ToString();
                            itmDouble.Tag = item.Count.ToString();
                            chkItmAutoLog.IsChecked = item.Last;
                        }
                        else
                        {
                            bodyImagesorce = "https://minotar.net/" + item.UUID;
                            itmRename.IsEnabled = false;
                            txtTypeSettings.Text = "Microsoft Account";
                            fnticoAcTypeSettings.Glyph = item.TypeIconGlyph;
                            prpSettings.DisplayName = item.UserName;
                            txtSettingsPrpName.Text = item.UserName;
                            chkItmAutoLog.Tag = item.Count.ToString();
                            itmRemove.Tag = item.Count.ToString();
                            btnCopyUUID.Tag = item.Count.ToString();
                            btnCopyToken.Tag = item.Count.ToString();
                            txtbxRename.Tag = item.Count.ToString();
                            itmDouble.Tag = item.Count.ToString();
                            chkItmAutoLog.IsChecked = item.Last;
                        }
                    }
                }
            }
            gridNew.Visibility = Visibility.Collapsed;
            gridChoose.Visibility = Visibility.Collapsed;
            gridSettingsOnline.Visibility = Visibility.Visible;
        }
        public string bodyImagesorce { get; set; }
        private void chkItmAutoLog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem itm)
            {
                if (itm.IsChecked)
                {
                    foreach (var item in vars.Accounts)
                    {
                        if (item.Last)
                        {
                            item.Last = false;
                        }
                    }
                }
                foreach (var item in vars.Accounts)
                {
                    if (item.Count == int.Parse(itm.Tag.ToString()))
                    {
                        item.Last = itm.IsChecked;
                        return;
                    }
                }
            }
        }

        private void itmRemove_Click(object sender, RoutedEventArgs e)
        {
            LogOutFromCache(sender, e);
            btnChooseAcc_Click(null, null);
        }

        private async void btnCopyUUID_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Count == int.Parse(btn.Tag.ToString()))
                    {
                        fnticoUUID.Glyph = "\xE8FB";
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(item.UUID);
                        Clipboard.SetContent(dataPackage);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                        fnticoUUID.Glyph = "\xE71B";
                        return;
                    }
                }
            }
        }

        private async void btnCopyToken_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Count == int.Parse(btn.Tag.ToString()))
                    {
                        fnticoToken.Glyph = "\xE8FB";
                        var dataPackage = new DataPackage();
                        dataPackage.SetText(item.AccessToken);
                        Clipboard.SetContent(dataPackage);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                        fnticoToken.Glyph = "\xE71B";
                        return;
                    }
                }
            }
        }

        private void itmRename_Click(object sender, RoutedEventArgs e)
        {
            txtSettingsPrpName.Visibility = Visibility.Collapsed;
            txtbxRename.Visibility = Visibility.Visible;
            txtbxRename.Text = txtSettingsPrpName.Text;
            txtbxRename.Focus(FocusState.Keyboard);
        }

        private void txtbxRename_LostFocus(object sender, RoutedEventArgs e)
        {
            Rename(txtbxRename);
        }

        private void txtbxRename_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if(e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender is TextBox btn)
                {
                    Rename(txtbxRename);
                }
            }
        }
        private void Rename(TextBox txtbx)
        {
            if (!string.IsNullOrEmpty(txtbx.Text))
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Count == int.Parse(txtbx.Tag.ToString()))
                    {
                        if (item.Type == "Offline")
                        {
                            if (vars.CurrentAccountCount == item.Count)
                            {
                                vars.session = MSession.GetOfflineSession(txtbxRename.Text);
                                vars.UserName = txtbxRename.Text;
                            }
                            item.UserName = txtbxRename.Text;
                            txtSettingsPrpName.Text = txtbxRename.Text;
                            txtSettingsPrpName.Visibility = Visibility.Visible;
                            txtbxRename.Visibility = Visibility.Collapsed;
                            UpdateAccounts();
                            return;
                        }
                    }
                }
            }
            else
            {
                txtSettingsPrpName.Visibility = Visibility.Visible;
                txtbxRename.Visibility = Visibility.Collapsed;
            }
        }

        private void itmDouble_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in vars.Accounts)
            {
                if (item.Count == int.Parse(itmDouble.Tag.ToString()))
                {
                    vars.Accounts.Add(new Account(item.UserName, item.Type, item.AccessToken, item.UUID, vars.AccountsCount + 1, false));
                    vars.AccountsCount++;
                    UpdateAccounts();
                    btnChooseAcc_Click(null, null);
                    return;
                }
            }
        }
    }
}
