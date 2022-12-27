using Emerald.Core;
using Emerald.Core.Tasks;
using Emerald.WinUI.Helpers;
using Emerald.WinUI.Models;
using Emerald.WinUI.UserControls;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Linq;
using Windows.UI;
using static System.Net.WebRequestMethods;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;
using Windows.Storage.Pickers;

namespace Emerald.WinUI.Views.Settings
{
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
            var fop = new FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(fop, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            var f = await fop.PickSingleFolderAsync();
            if(f != null)
            {
                SS.Settings.Minecraft.Path = f.Path;
            }
        }

        private void btnRamPlus_Click(object sender, RoutedEventArgs e) =>
            SS.Settings.Minecraft.RAM = SS.Settings.Minecraft.RAM + 50;
        

        private void btnRamMinus_Click(object sender, RoutedEventArgs e) =>
            SS.Settings.Minecraft.RAM = SS.Settings.Minecraft.RAM - 50;
        

        private void btnAutoRAM_Click(object sender, RoutedEventArgs e)=>
            SS.Settings.Minecraft.RAM = DirectResoucres.MaxRAM / 2;
        

    }
}
