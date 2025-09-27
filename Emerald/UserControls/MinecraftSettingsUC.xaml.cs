using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace Emerald.UserControls;
public sealed partial class MinecraftSettingsUC : UserControl
{
    public bool ShowMainSettings
    {
        get => (bool)GetValue(ShowMainSettingsProperty);
        set => SetValue(ShowMainSettingsProperty, value);
    }

    public static readonly DependencyProperty ShowMainSettingsProperty =
        DependencyProperty.Register(nameof(ShowMainSettings), typeof(bool), typeof(MinecraftSettingsUC), new PropertyMetadata(false));

    public GameSettings GameSettings
    {
        get => (GameSettings)GetValue(GameSettingsProperty);
        set => SetValue(GameSettingsProperty, value);
    }

    public static readonly DependencyProperty GameSettingsProperty =
        DependencyProperty.Register(nameof(GameSettings), typeof(GameSettings), typeof(MinecraftSettingsUC), new PropertyMetadata(null));

    // expose SS as a public property if you bind to it from x:Bind in XAML, otherwise x:Bind may not resolve.
    public Services.SettingsService SS { get; }

    public MinecraftSettingsUC()
    {
        InitializeComponent();
        SS = Ioc.Default.GetService<Services.SettingsService>();
    }

    // helper method so we can call from multiple handlers
    private async Task PickMinecraftFolderAsync()
    {
        this.Log().LogInformation("Choosing MC path");

        var fop = new FolderPicker { CommitButtonText = "Select".Localize() };
        fop.FileTypeFilter.Add("*");

        if (DirectResoucres.Platform == "Windows")
            WinRT.Interop.InitializeWithWindow.Initialize(fop, WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));

        var f = await fop.PickSingleFolderAsync();

        if (f == null)
        {
            this.Log().LogInformation("User did not select a MC path");
            return;
        }

        var path = f.Path;
        this.Log().LogInformation("New Minecraft path: {path}", path);
        SS.Settings.Minecraft.Path = path;

        await Ioc.Default.GetService<CoreX.Core>().InitializeAndRefresh(new(path));
    }

    private async void btnChangeMPath_Click(object sender, RoutedEventArgs e)
    {
        await PickMinecraftFolderAsync();
    }

    private async void ChangePath_OnClick(object sender, RoutedEventArgs e)
    {
        // call the same helper instead of calling the handler with nulls
        await PickMinecraftFolderAsync();
    }

    private void CopyPath_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = ShowMainSettings ? SS.Settings.Minecraft.Path : Path.Combine(SS.Settings.Minecraft.Path, CoreX.Core.GamesFolderName);
            var dp = new DataPackage();
            dp.SetText(path);
            Clipboard.SetContent(dp);
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to copy path");
        }
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

    private void btnRamPlus_Click(object sender, RoutedEventArgs e) => AdjustRam(64);
    private void btnRamMinus_Click(object sender, RoutedEventArgs e) => AdjustRam(-64);

    private void btnAutoRAM_Click(object sender, RoutedEventArgs e)
    {
        int sysMax = DirectResoucres.MaxRAM;

        int recommended = sysMax switch
        {
            <= 4096 => DirectResoucres.MinRAM,
            <= 8192 => sysMax / 3,
            <= 16384 => sysMax / 2,
            _ => (int)(sysMax * 0.65)
        };

        GameSettings.MaximumRamMb = recommended;
    }
}
