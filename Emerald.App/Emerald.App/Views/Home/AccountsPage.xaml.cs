using Emerald.WinUI.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

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

        private void ToggleMenuFlyoutItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Accounts.Where(x => ((sender as ToggleMenuFlyoutItem).DataContext as Account).Count == x.Count).FirstOrDefault().IsChecked = (sender as ToggleMenuFlyoutItem).IsChecked;
        }

        private void Button_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Accounts.Where(x=> ((sender as Button).DataContext as Account).Count == x.Count).FirstOrDefault().CheckboxVsibility = Microsoft.UI.Xaml.Visibility.Visible;
        }

        private void Button_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            CheckAndHideCheckBox(((sender as Button).DataContext as Account).Count);
        }
        private void CheckAndHideCheckBox(int count)
        {
            var x = Accounts.Where(x => x.Count == count).FirstOrDefault();
            x.CheckboxVsibility =
            x.IsChecked ? Visibility.Visible
            : ((Accounts.Count > 1 && Accounts.Any(x => x.IsChecked)) ? Visibility.Visible : Visibility.Collapsed);
        }
        private void CheckBox_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Accounts.Where(x => ((sender as CheckBox).DataContext as Account).Count == x.Count).FirstOrDefault().IsChecked = (sender as CheckBox).IsChecked.Value;
        }
    }
}
