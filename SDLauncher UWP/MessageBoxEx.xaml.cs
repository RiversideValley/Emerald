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

namespace SDLauncher_UWP
{
    public sealed partial class MessageBoxEx : ContentDialog
    {
        public Results Result { get; set; }
        public enum Results
        {
            Ok,
            Cancel,
            Yes,
            No
        }
        public enum Buttons
        {
            Ok,
            OkCancel,
            YesNo
        }
        public MessageBoxEx(string title,string caption, Buttons buttons)
        {
            this.InitializeComponent();
            Title = title;
            txt.Text = caption;
            if (buttons == Buttons.Ok)
            {
                PrimaryButtonText = "";
                SecondaryButtonText = "OK";
            }
            else if (buttons == Buttons.OkCancel)
            {
                PrimaryButtonText = "OK";
                SecondaryButtonText = "Cancel";
            }
            else if (buttons == Buttons.YesNo)
            {
                PrimaryButtonText = "Yes";
                SecondaryButtonText = "No";
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (sender.PrimaryButtonText == "OK")
            {
                Result = Results.Ok;
            }
            else if (sender.PrimaryButtonText == "Yes")
            {
                Result = Results.Yes;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (sender.SecondaryButtonText == "OK")
            {
                Result = Results.Ok;
            }
            else if (sender.SecondaryButtonText == "Cancel")
            {
                Result = Results.Cancel;
            }
            else if (sender.SecondaryButtonText == "No")
            {
                Result = Results.No;
            }
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (vars.theme != null)
            {
                if (Window.Current.Content is FrameworkElement fe)
                {
                    this.RequestedTheme = (ElementTheme)vars.theme;
                    fe.RequestedTheme = (ElementTheme)vars.theme;
                }
            }

        }
    }
}
