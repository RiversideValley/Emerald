using Emerald.Core;
using Emerald.WinUI.Enums;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers
{
    /// <summary>
    /// A <see cref="Microsoft.UI.Xaml.Controls.ContentDialog"/> as a MessageBox...  
    /// Copied From the Emerald.UWP
    /// </summary>
    public class MessageBox : ContentDialog
    {
        public MessageBoxResults Result { get; set; }
        public MessageBox(string title, string caption, MessageBoxButtons buttons, string cusbtn1 = null, string cusbtn2 = null)
        {
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            Title = title;
            this.Content = new CommunityToolkit.WinUI.UI.Controls.MarkdownTextBlock() { Text = caption, Background = new SolidColorBrush(Colors.Transparent) };
            if (buttons == MessageBoxButtons.Ok)
            {
                PrimaryButtonText = "";
                SecondaryButtonText = Localized.OK.ToLocalizedString();
                DefaultButton = ContentDialogButton.None;
            }
            else if (buttons == MessageBoxButtons.OkCancel)
            {
                PrimaryButtonText = Localized.OK.ToLocalizedString();
                SecondaryButtonText = Localized.Cancel.ToLocalizedString();
                DefaultButton = ContentDialogButton.Primary;
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                PrimaryButtonText = Localized.Yes.ToLocalizedString();
                SecondaryButtonText = Localized.No.ToLocalizedString();
                DefaultButton = ContentDialogButton.Primary;
            }
            else if (buttons == MessageBoxButtons.Custom)
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
                    PrimaryButtonText = Localized.Yes.ToLocalizedString();
                    SecondaryButtonText = Localized.No.ToLocalizedString();
                    DefaultButton = ContentDialogButton.Primary;
                }
            }
            else if (buttons == MessageBoxButtons.CustomWithCancel)
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
                    DefaultButton = ContentDialogButton.Primary;
                    PrimaryButtonText = Localized.Yes.ToLocalizedString();
                    SecondaryButtonText = Localized.No.ToLocalizedString();
                }
                CloseButtonText = Localized.Cancel.ToLocalizedString();
            }
            PrimaryButtonClick += ContentDialog_PrimaryButtonClick;
            SecondaryButtonClick += ContentDialog_SecondaryButtonClick;
        }
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (sender.PrimaryButtonText == Localized.OK.ToLocalizedString())
            {
                Result = MessageBoxResults.Ok;
            }
            else if (sender.PrimaryButtonText == Localized.Yes.ToLocalizedString())
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
            if (sender.SecondaryButtonText == Localized.OK.ToLocalizedString())
            {
                Result = MessageBoxResults.Ok;
            }
            else if (sender.SecondaryButtonText == Localized.Cancel.ToLocalizedString())
            {
                Result = MessageBoxResults.Cancel;
            }
            else if (sender.SecondaryButtonText == Localized.No.ToLocalizedString())
            {
                Result = MessageBoxResults.No;
            }
            else
            {
                Result = MessageBoxResults.CustomResult2;
            }
        }
        public static async Task<MessageBoxResults> Show(string title, string caption, MessageBoxButtons buttons, string customResult1 = null, string customResult2 = null)
        {
            var d = new MessageBox(title, caption, buttons, customResult1, customResult2);
            d.XamlRoot = MainWindow.HomePage.XamlRoot;
            await d.ShowAsync();
            return d.Result;
        }
        public static async Task<MessageBoxResults> Show(string text)
        {
            var d = new MessageBox("Information", text, MessageBoxButtons.Ok);
            d.XamlRoot = MainWindow.HomePage.XamlRoot;
            await d.ShowAsync();
            return d.Result;
        }
    }
}
