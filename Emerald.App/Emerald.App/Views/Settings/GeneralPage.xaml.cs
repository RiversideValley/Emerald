using CmlLib.Core;
using Emerald.WinUI.Enums;
using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.Storage.Pickers;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

namespace Emerald.WinUI.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GeneralPage : Page
    {
        public GeneralPage()
        {
            InitializeComponent();
        }

        private async void btnChangeMPath_Click(object sender, RoutedEventArgs e)
        {
            MinecraftPath mcP;
            bool retryMC = true;
            while (retryMC)
            {
                try
                {
                    mcP = new(SS.Settings.Minecraft.Path);
                    retryMC = false;
                }
                catch
                {
                    var r = await MessageBox.Show("Error".Localize(), "MCPathFailed".Localize().Replace("{Path}", SS.Settings.Minecraft.Path), MessageBoxButtons.Custom, "Yes".Localize(), "SetDifMCPath".Localize());

                   if (r == MessageBoxResults.CustomResult2)
                    {
                        var fop = new FolderPicker
                        {
                            CommitButtonText = "Select".Localize()
                        };
                        WinRT.Interop.InitializeWithWindow.Initialize(fop, WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));
                        var f = await fop.PickSingleFolderAsync();

                        if (f != null)
                            SS.Settings.Minecraft.Path = f.Path;
                    }
                }

            }
        }

        private void btnRamPlus_Click(object sender, RoutedEventArgs e) =>
            SS.Settings.Minecraft.RAM = SS.Settings.Minecraft.RAM + (SS.Settings.Minecraft.RAM >= DirectResoucres.MaxRAM ? 0 : (DirectResoucres.MaxRAM - SS.Settings.Minecraft.RAM >= 50 ? 50 : DirectResoucres.MaxRAM - SS.Settings.Minecraft.RAM) );


        private void btnRamMinus_Click(object sender, RoutedEventArgs e) =>
            SS.Settings.Minecraft.RAM = SS.Settings.Minecraft.RAM - (SS.Settings.Minecraft.RAM <= DirectResoucres.MinRAM ? 0 : 50);


        private void btnAutoRAM_Click(object sender, RoutedEventArgs e) =>
            SS.Settings.Minecraft.RAM = DirectResoucres.MaxRAM / 2;


    }
}
