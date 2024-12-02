using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CmlLib.Core;
using CommunityToolkit.WinUI.Controls;
using Emerald.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;

using SS = Emerald.Helpers.Settings.SettingsSystem;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Emerald.Views.Settings;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GeneralPage : Page
{
    public GeneralPage()
    {
        this.InitializeComponent();
    }
    private async void btnChangeMPath_Click(object sender, RoutedEventArgs e)
    {
        this.Log().LogInformation("Choosing MC path");
        string path;

        var fop = new FolderPicker
        {
            CommitButtonText = "Select".Localize()
        };
        fop.FileTypeFilter.Add("*");

        var f = await fop.PickSingleFolderAsync();

        if (f != null)
            path = f.Path;
        else
        {
            this.Log().LogInformation("User did not select a MC path");
            return;
        }

        this.Log().LogInformation("New Minecraft path: {path}",path);
        SS.Settings.Minecraft.Path = path;
    }

    private void btnRamPlus_Click(object sender, RoutedEventArgs e) =>
        SS.Settings.Minecraft.RAM = SS.Settings.Minecraft.RAM + (SS.Settings.Minecraft.RAM >= DirectResoucres.MaxRAM ? 0 : (DirectResoucres.MaxRAM - SS.Settings.Minecraft.RAM >= 50 ? 50 : DirectResoucres.MaxRAM - SS.Settings.Minecraft.RAM));


    private void btnRamMinus_Click(object sender, RoutedEventArgs e) =>
        SS.Settings.Minecraft.RAM = SS.Settings.Minecraft.RAM - (SS.Settings.Minecraft.RAM <= DirectResoucres.MinRAM ? 0 : 50);


    private void btnAutoRAM_Click(object sender, RoutedEventArgs e) =>
        SS.Settings.Minecraft.RAM = DirectResoucres.MaxRAM / 2;
}
