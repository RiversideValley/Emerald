using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
    public enum MessageBoxResults
    {
        Ok,
        Cancel,
        Yes,
        No
    }
    public enum MessageBoxButtons
    {
        Ok,
        OkCancel,
        YesNo
    }
    public sealed partial class MessageBoxEx : ContentDialog
    {
        public MessageBoxResults Result { get; set; }
        public MessageBoxEx(string title, string caption, MessageBoxButtons buttons)
        {
            this.InitializeComponent();
            Title = title;
            txt.Text = caption;
            if (buttons == MessageBoxButtons.Ok)
            {
                PrimaryButtonText = "";
                SecondaryButtonText = "OK";
            }
            else if (buttons == MessageBoxButtons.OkCancel)
            {
                PrimaryButtonText = "OK";
                SecondaryButtonText = "Cancel";
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                PrimaryButtonText = "Yes";
                SecondaryButtonText = "No";
            }
            this.RequestedTheme = (ElementTheme)vars.theme;
        }
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (sender.PrimaryButtonText == "OK")
            {
                Result = MessageBoxResults.Ok;
            }
            else if (sender.PrimaryButtonText == "Yes")
            {
                Result = MessageBoxResults.Yes;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (sender.SecondaryButtonText == "OK")
            {
                Result = MessageBoxResults.Ok;
            }
            else if (sender.SecondaryButtonText == "Cancel")
            {
                Result = MessageBoxResults.Cancel;
            }
            else if (sender.SecondaryButtonText == "No")
            {
                Result = MessageBoxResults.No;
            }
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
    public class MessageBox
    {
        public static async Task<MessageBoxResults> Show(string title,string caption, MessageBoxButtons buttons)
        {
            var d = new MessageBoxEx(title, caption, buttons);
            await d.ShowAsync();
            return d.Result;
        }
    }
}
