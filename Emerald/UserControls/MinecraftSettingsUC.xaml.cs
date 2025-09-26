using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Emerald.UserControls;
public sealed partial class MinecraftSettingsUC : UserControl
{
    public bool ShowMainPathEditor
    {
        get { return (bool)GetValue(ShowMainPathEditorProperty); }
        set { SetValue(ShowMainPathEditorProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ShowMainPathEditor.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ShowMainPathEditorProperty =
        DependencyProperty.Register("ShowMainPathEditor", typeof(bool), typeof(MinecraftSettingsUC), new PropertyMetadata(null));

    public GameSettings GameSettings
    {
        get { return (GameSettings)GetValue(GameSettingsProperty); }
        set { SetValue(GameSettingsProperty, value); }
    }

    // Using a DependencyProperty as the backing store for GameSettings.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty GameSettingsProperty =
        DependencyProperty.Register("GameSettings", typeof(GameSettings), typeof(MinecraftSettingsUC), new PropertyMetadata(null));


    private readonly Services.SettingsService SS;
    public MinecraftSettingsUC()
    {
        SS = Ioc.Default.GetService<Services.SettingsService>();
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

        if (DirectResoucres.Platform == "Windows")
            WinRT.Interop.InitializeWithWindow.Initialize(fop, WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));

        var f = await fop.PickSingleFolderAsync();

        if (f != null)
            path = f.Path;
        else
        {
            this.Log().LogInformation("User did not select a MC path");
            return;
        }

        this.Log().LogInformation("New Minecraft path: {path}", path);
        SS.Settings.Minecraft.Path = path;

        await Ioc.Default.GetService<CoreX.Core>().InitializeAndRefresh(new(path));
    }

    private void AdjustRam(int delta)
    {
        int newValue = GameSettings.MaximumRamMb + delta;

        GameSettings.MaximumRamMb = Math.Clamp(
            newValue,
            DirectResoucres.MinRAM,
            DirectResoucres.MaxRAM
        );
    }

    private void btnRamPlus_Click(object sender, RoutedEventArgs e) =>
        AdjustRam(64);

    private void btnRamMinus_Click(object sender, RoutedEventArgs e) =>
        AdjustRam(-64);

    private void btnAutoRAM_Click(object sender, RoutedEventArgs e)
    {
        int sysMax = DirectResoucres.MaxRAM;

        int recommended = sysMax switch
        {
            <= 4096 => DirectResoucres.MinRAM,  // low-memory PCs
            <= 8192 => sysMax / 3,             // mid-range
            <= 16384 => sysMax / 2,            // standard gaming rigs
            _ => (int)(sysMax * 0.65)          // high RAM → ~65%
        };

        GameSettings.MaximumRamMb = recommended;
    }

}
