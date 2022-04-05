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
                vars.session = tempsession;
                vars.UserName = tempsession.Username;
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

        private async void LoginFromCache(object sender, RoutedEventArgs e)
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
                    }
                }
            }
        }
        private void LogOutFromCache(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Count == int.Parse(btn.Tag.ToString()))
                    {
                        if (item.Type == "Offline")
                        {
                            if(vars.session.Username == item.UserName && vars.session.UserType == "Mojang")
                            {
                                vars.session = null;
                                vars.UserName = "";
                            }
                        }
                        else
                        {
                            if (vars.session.Username == item.UserName && vars.session.UserType == item.Type && vars.session.AccessToken == item.AccessToken && vars.session.UUID == item.UUID)
                            {
                                vars.session = null;
                                vars.UserName = "";
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
                vars.Accounts.Add(new Account(session.Username, "Offline", "null", "null", vars.Accounts.Count + 1, true));
            }
            else
            {
                vars.Accounts.Add(new Account(session.Username, session.UserType, session.AccessToken, session.UUID, vars.Accounts.Count + 1, true));
            }
            vars.session = session;
            vars.UserName = session.Username;
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
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            gridNew.Visibility = Visibility.Visible;
            gridChoose.Visibility = Visibility.Collapsed;
            gridSettingsOnline.Visibility = Visibility.Collapsed;
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
