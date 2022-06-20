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

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP.Dialogs
{
    public sealed partial class StoreItemInstallDialog : ContentDialog
    {
        public StoreItemInstallDialog()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        //public void SelectItem(StoreItem itm)
        //{
        //    gridChoosed.Visibility = Visibility.Visible;
        //    txtDescription.Text = itm.Description;
        //    txtName.Text = itm.Name;
        //    imsFeatures.ItemsSource = itm.Features;
        //    ItemsCollection.Visibility = Visibility.Collapsed;
        //    flipSamples.ItemsSource = itm.SampleImages;
        //}
    }
}
