using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.Helpers;
using Emerald.Helpers.Enums;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;

namespace Emerald.Helpers;

/// <summary>
/// A <see cref="ContentDialog"/> as a MessageBox...<br/>
/// Copied From the Emerald.UWP
/// </summary>
public partial class MessageBox : ContentDialog
{
    public MessageBoxResults Result { get; set; } = MessageBoxResults.Cancel;

    public MessageBox(string title, string caption, MessageBoxButtons buttons, string cusbtn1 = null, string cusbtn2 = null)
    {
        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        Title = title;
        Content = new TextBlock { Text = caption };

        if (buttons == MessageBoxButtons.Ok)
        {
            PrimaryButtonText = "";
            SecondaryButtonText = "OK".Localize();
            DefaultButton = ContentDialogButton.None;
        }
        else if (buttons == MessageBoxButtons.OkCancel)
        {
            PrimaryButtonText = "OK".Localize();
            SecondaryButtonText = "Cancel".Localize();
            DefaultButton = ContentDialogButton.Primary;
        }
        else if (buttons == MessageBoxButtons.YesNoCancel)
        {
            PrimaryButtonText = "Yes".Localize();
            SecondaryButtonText = "No".Localize();
            CloseButtonText = "Cancel".Localize();
            DefaultButton = ContentDialogButton.Primary;
        }
        else if (buttons == MessageBoxButtons.YesNo)
        {
            PrimaryButtonText = "Yes".Localize();
            SecondaryButtonText = "No".Localize();
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
                PrimaryButtonText = "Yes".Localize();
                SecondaryButtonText = "No".Localize();
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
                PrimaryButtonText = "Yes".Localize();
                SecondaryButtonText = "No".Localize();
            }

            CloseButtonText = "Cancel".Localize();
        }

        PrimaryButtonClick += ContentDialog_PrimaryButtonClick;
        SecondaryButtonClick += ContentDialog_SecondaryButtonClick;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (sender.PrimaryButtonText == "OK".Localize())
        {
            Result = MessageBoxResults.Ok;
        }
        else if (sender.PrimaryButtonText == "Yes".Localize())
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
        if (sender.SecondaryButtonText == "OK".Localize())
        {
            Result = MessageBoxResults.Ok;
        }
        else if (sender.SecondaryButtonText == "Cancel".Localize())
        {
            Result = MessageBoxResults.Cancel;
        }
        else if (sender.SecondaryButtonText == "No".Localize())
        {
            Result = MessageBoxResults.No;
        }
        else
        {
            Result = MessageBoxResults.CustomResult2;
        }
    }

    public static async Task<MessageBoxResults> Show(string title, string caption, MessageBoxButtons buttons, string customResult1 = null, string customResult2 = null, bool waitUntilOpens = true)
    {
        var theme = ServiceLocator.IsLocationProviderSet ? 

            (ElementTheme)Ioc.Default.GetService<Services.SettingsService>().Settings.App.Appearance.Theme :
            ElementTheme.Default;
        var d = new MessageBox(title, caption, buttons, customResult1, customResult2)
        {
            XamlRoot = App.Current.MainWindow.Content.XamlRoot,
            RequestedTheme = theme
        };

        if (waitUntilOpens)
        {
            bool notOpen = true;
            while (notOpen)
            {
                try
                {
                    await d.ShowAsync();
                    notOpen = false;
                }
                catch (NullReferenceException) // XamlRoot can be null
                {
                    notOpen = false;
                    return MessageBoxResults.OpenFailed;
                }
            }
            return d.Result;
        }

        try
        {
            await d.ShowAsync();
        }
        catch
        {
            return MessageBoxResults.OpenFailed;
        }

        return d.Result;
    }

    public static async Task<MessageBoxResults> Show(string text)
    {
        var theme = ServiceLocator.IsLocationProviderSet ?


            (ElementTheme)Ioc.Default.GetService<Services.SettingsService>().Settings.App.Appearance.Theme :
            ElementTheme.Default;
        var d = new MessageBox("Information".Localize(), text, MessageBoxButtons.Ok)
        {
            XamlRoot = App.Current.MainWindow.Content.XamlRoot,
            RequestedTheme = theme
        };

        try
        {
            await d.ShowAsync();
        }
        catch
        {
            return MessageBoxResults.OpenFailed;
        }

        return d.Result;
    }
}
