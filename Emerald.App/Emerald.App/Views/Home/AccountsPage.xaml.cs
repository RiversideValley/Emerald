using Emerald.WinUI.Models;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
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
    }
}
