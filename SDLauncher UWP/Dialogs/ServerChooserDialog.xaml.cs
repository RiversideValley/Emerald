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
using Windows.UI.Xaml.Navigation;
using SDLauncher_UWP.DataTemplates;
using System.Collections.ObjectModel;
// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP.Dialogs
{
    public sealed partial class ServerChooserDialog : ContentDialog
    {
        public ObservableCollection<ServerTemplate> servers = new ObservableCollection<ServerTemplate>();
        public ServerChooserDialog()
        {
            this.InitializeComponent();
            servers.Add(new ServerTemplate("mc.hypixel.net", 25565));
            view.ItemsSource = servers;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
