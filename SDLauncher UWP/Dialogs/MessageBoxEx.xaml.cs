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

namespace SDLauncher.UWP
{
    public enum MessageBoxResults
    {
        Ok,
        Cancel,
        Yes,
        No,
        CustomResult1,
        CustomResult2
    }
    public enum MessageBoxButtons
    {
        Ok,
        OkCancel,
        YesNo,
        Custom,
        CustomWithCancel
    }
    public sealed partial class MessageBoxEx : ContentDialog
    {
        public MessageBoxResults Result { get; set; }
        public MessageBoxEx(string title, string caption, MessageBoxButtons buttons, string cusbtn1 = null, string cusbtn2 = null)
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
            else if (buttons == MessageBoxButtons.Custom)
            {
                if(!string.IsNullOrEmpty(cusbtn1))
                {
                    PrimaryButtonText = cusbtn1;
                }
                if(!string.IsNullOrEmpty(cusbtn2))
                {
                    SecondaryButtonText = cusbtn2;
                }
                if(string.IsNullOrEmpty(cusbtn2) && string.IsNullOrEmpty(cusbtn1))
                {
                    PrimaryButtonText = "Yes";
                    SecondaryButtonText = "No";
                }
            }else if(buttons == MessageBoxButtons.CustomWithCancel)
            {
                if (!string.IsNullOrEmpty(cusbtn1))
                {
                    PrimaryButtonText = cusbtn1;
                }
                if (!string.IsNullOrEmpty(cusbtn2))
                {
                    SecondaryButtonText = cusbtn2;
                }
                if (string.IsNullOrEmpty(cusbtn2) && string.IsNullOrEmpty(cusbtn1))
                {
                    PrimaryButtonText = "Yes";
                    SecondaryButtonText = "No";
                }
                CloseButtonText = "Cancel";
            }
            
            this.RequestedTheme = (ElementTheme)vars.Theme;
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
            else
            {
                Result = MessageBoxResults.CustomResult1;
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
            else
            {
                Result = MessageBoxResults.CustomResult2;
            }
        }

        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }
    }
    public class MessageBox
    {
        public static async Task<MessageBoxResults> Show(string title, string caption, MessageBoxButtons buttons, string customResult1 = null, string customResult2 = null)
        {
            var d = new MessageBoxEx(title, caption, buttons, customResult1, customResult2);
            await d.ShowAsync();
            return d.Result;
        }
        public static async Task<MessageBoxResults> Show(string text)
        {
            var d = new MessageBoxEx("Information", text, MessageBoxButtons.Ok);
            await d.ShowAsync();
            return d.Result;
        }
    }
}
