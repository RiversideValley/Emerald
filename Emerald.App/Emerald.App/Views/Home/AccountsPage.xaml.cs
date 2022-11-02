using Emerald.WinUI.Models;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace Emerald.WinUI.Views.Home
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AccountsPage : Page
    {
        public readonly ObservableCollection<Account> Accounts = new();
        public int AllCount = 0;
        public AccountsPage()
        {
            this.InitializeComponent();
            gv.ItemsSource = Accounts;
        }

        private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            //Accounts.Where(x => ((sender as ToggleMenuFlyoutItem).DataContext as Account).Count == x.Count).FirstOrDefault().IsChecked = (sender as ToggleMenuFlyoutItem).IsChecked;
            UpdateAll();
        }

        private void btnAccount_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Accounts.Where(x => ((sender as Button).DataContext as Account).Count == x.Count).FirstOrDefault().CheckBoxLoaded = true;
        }

        private void btnAccount_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            CheckAndHideCheckBox(((sender as Button).DataContext as Account).Count);
        }
        private void CheckAndHideCheckBox(int count)
        {
            var x = Accounts.Where(x => x.Count == count).FirstOrDefault();
            x.CheckBoxLoaded =
            x.IsChecked || (Accounts.Count > 1 && Accounts.Any(x => x.IsChecked));
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
        }
        private async void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Accounts.Where(x => ((sender as CheckBox).DataContext as Account).Count == x.Count).FirstOrDefault().IsChecked = (sender as CheckBox).IsChecked.Value;
                UpdateAll();
            }catch { }
        }

        private void btnAccount_Click(object sender, RoutedEventArgs e)
        {
            if (Accounts.Any(x => x.IsChecked))
            {
                ((sender as Button).DataContext as Account).IsChecked = !((sender as Button).DataContext as Account).IsChecked;
                UpdateAll();
            }
        }
    }
}
