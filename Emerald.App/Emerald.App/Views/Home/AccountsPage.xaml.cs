using CmlLib.Core.Auth;
using Emerald.Core;
using Emerald.WinUI.Helpers;
using Emerald.WinUI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Windows.Storage;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

namespace Emerald.WinUI.Views.Home;

public sealed partial class AccountsPage : Page, INotifyPropertyChanged
{
    public event EventHandler? AccountLogged;
    public event EventHandler? BackRequested;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Set<T>(ref T obj, T value, string name = null)
    {
        obj = value;
        InvokePropertyChanged(name);
    }
    public void InvokePropertyChanged(string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    private bool ShowEditor => EditorAccount != null;
    private Account _EditorAccount = null;
    private Account EditorAccount
    {
        get => _EditorAccount;
        set => Set(ref _EditorAccount, value);
    }
    public readonly ObservableCollection<Account> Accounts = new();
    public int AllCount = 0;
    public AccountsPage()
    {
        InitializeComponent();
        gv.ItemsSource = Accounts;
    }

    private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        //Accounts.Where(x => ((sender as ToggleMenuFlyoutItem).DataContext as Account).Count == x.Count).FirstOrDefault().IsChecked = (sender as ToggleMenuFlyoutItem).IsChecked;
        UpdateAll();
    }

    private void btnAccount_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Accounts.FirstOrDefault(x => ((sender as Button).DataContext as Account).Count == x.Count).CheckBoxLoaded = true;
    }

    private void btnAccount_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        CheckAndHideCheckBox(Accounts.IndexOf((sender as Button).DataContext as Account));
    }
    private void CheckAndHideCheckBox(int count)
    {
        try
        {
            var x = Accounts[count];
            x.CheckBoxLoaded =
            x.IsChecked || (Accounts.Count > 1 && Accounts.Any(x => x.IsChecked));
        }
        catch { }
    }
    private void UpdateAll()
    {
        for (int i = Accounts.Count - 1; i >= 0; i--)
        {
            try
            {
                CheckAndHideCheckBox(i);
            }
            catch { }
        }
        btnSelect.Label = Accounts.Where(x => x.IsChecked).Any() ? Core.Localized.SelectNone.Localize() : Core.Localized.SelectAll.Localize();
        btnSelect.Icon = new SymbolIcon(Accounts.Where(x => x.IsChecked).Any() ? Symbol.ClearSelection : Symbol.SelectAll);
    }
    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            //Accounts.Where(x => ((sender as CheckBox).DataContext as Account).Count == x.Count).FirstOrDefault().IsChecked = (sender as CheckBox).IsChecked.Value;
            UpdateAll();
        }
        catch { }
    }

    private void btnAccount_Click(object sender, RoutedEventArgs e)
    {
        var a = (sender as Button).DataContext as Account;
        if (Accounts.Any(x => x.IsChecked))
        {
            a.IsChecked = !a.IsChecked;
            UpdateAll();
        }
        else
        {
            Accounts.Remove(a);
            if (EditorAccount != null)
            {
                Accounts.Add(EditorAccount);
                SetEditor(null);
            }
            SetEditor(a);
        }
    }
    private void SetEditor(Account account) =>
        EditorAccount = account;



    private void btnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        bool val = !Accounts.Where(x => x.IsChecked).Any();
        foreach (var item in Accounts)
        {
            item.IsChecked = val;
        }
        UpdateAll();

    }
    private void RemoveSelected() =>
        Accounts.Remove(x => x.IsChecked);

    private void btnRemove_Click(object sender, RoutedEventArgs e) =>
        RemoveSelected();


    private void mitRemove_Click(object sender, RoutedEventArgs e)
    {
        if (Accounts.Where(x => x.IsChecked).Any())
        {
            RemoveSelected();
        }
        else
        {
            Accounts.Remove((sender as MenuFlyoutItem).DataContext as Account);
        }
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        var a = (sender as Button).DataContext as Account;
        SetEditor(null);
        MainWindow.HomePage.Session = a.ToMSession();
        foreach (var item in Accounts)
            item.Last = false;

        a.Last = true;
        Accounts.Add(a);
        AccountLogged?.Invoke(this, new());
    }

    private void CancelLogin_Click(object sender, RoutedEventArgs e)
    {
        var a = (sender as Button).DataContext as Account;
        SetEditor(null);
        Accounts.Add(a);

    }
    public void UpdateMainSource() =>
        SS.Accounts = Accounts.Select(x =>
        new Helpers.Settings.JSON.Account()
        {
            AccessToken = x.AccessToken,
            LastAccessed = x.Last,
            Type = x.Type.ToString(),
            Username = x.UserName,
            UUID = x.UUID,
            ClientToken = x.ClientToken
        }).ToArray();

    public void UpdateSource()
    {
        Accounts.Clear();
        AllCount = 0;

        if (SS.Accounts != null)
        {
            foreach (var item in SS.Accounts)
            {
                Accounts.Add(new Account(item.Username, item.AccessToken, item.UUID, AllCount++, item.LastAccessed));
            }
        }
    }
    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (EditorAccount != null)
        {
            Accounts.Add(EditorAccount);
            SetEditor(null);
        }
        BackRequested?.Invoke(this, new EventArgs());
    }

    private void RemoveinEditor_Click(object sender, RoutedEventArgs e)
    {
        SetEditor(null);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        var ac = (sender as MenuFlyoutItem).DataContext as Account;
        var account = new Account(ac.UserName, null, null, AllCount++, false);
        Accounts.Add(account);
    }

    private void mitOfflineAdd_Click(object sender, RoutedEventArgs e)
    {
        if (EditorAccount != null)
        {
            Accounts.Add(EditorAccount);
            SetEditor(null);
        }
        SetEditor(MSession.GetOfflineSession(Localized.NewAccount.Localize()).ToAccount());
    }

    private async void mitMicrosoftAdd_Click(object sender, RoutedEventArgs e)
    {
        var taskID = Core.Tasks.TasksHelper.AddTask(Localized.LoginWithMicrosoft);
        var cId = await FileIO.ReadTextAsync(await StorageFile.GetFileFromPathAsync($"{Windows.ApplicationModel.Package.Current.InstalledPath}\\MsalClientID.txt"));
        var msl = new MSLoginHelper(cId, (sender as MenuFlyoutItem).Tag.ToString()
            switch
        {
            "DeviceCode" => MSLoginHelper.OAuthMode.DeviceCode,
            "Browser" => MSLoginHelper.OAuthMode.FromBrowser,
            _ => MSLoginHelper.OAuthMode.EmbededDialog
        });
        try
        {
            var r = await msl.Login();
            if (Accounts.Any(x => x.UUID == r.UUID))
            {
                if (await MessageBox.Show(Localized.MergeAccount.Localize(), Localized.MergeMsAcExistingWithNew.Localize().Replace("{Count}", Accounts.Count(x => x.UUID == r.UUID).ToString()), Enums.MessageBoxButtons.YesNo) == Enums.MessageBoxResults.Yes)
                    Accounts.Remove(x => x.UUID != r.UUID);
            }
            SetEditor(r.ToAccount());
            Core.Tasks.TasksHelper.CompleteTask(taskID);
        }
        catch (Exception ex)
        {

            Core.Tasks.TasksHelper.CompleteTask(taskID, false, ex.Message);
        }
    }
}
