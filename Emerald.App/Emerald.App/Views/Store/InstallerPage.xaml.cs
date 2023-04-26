using Emerald.WinUI.Models;
using Microsoft.UI.Xaml.Controls;


namespace Emerald.WinUI.Views.Store
{
    public sealed partial class InstallerPage : Page
    {
        public StoreItem Item { get; set; }
        public InstallerPage()
        {
            InitializeComponent();
        }
    }
}
