using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CmlLib.Core;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
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

using SS = Emerald.Services.SettingsService;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Emerald.Views.Settings;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GeneralPage : Page
{
    private readonly Services.SettingsService SS;
    public GeneralPage()
    {
        SS = Ioc.Default.GetService<Services.SettingsService>();
        this.InitializeComponent();
        MCUC.GameSettings = SS.Settings.GameSettings;
    }

}
