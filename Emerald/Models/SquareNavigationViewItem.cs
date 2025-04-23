using System.ComponentModel;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.Models;

public partial class SquareNavigationViewItem : Model
{

/* Unmerged change from project 'Emerald (net8.0-windows10.0.22621)'
Before:
    private readonly Helpers.Settings.SettingsSystem SS;
    public SquareNavigationViewItem()
After:
    private readonly SettingsSystem SS;
    public SquareNavigationViewItem()
*/
    private readonly Services.SettingsService SS;
    public SquareNavigationViewItem()
    {

/* Unmerged change from project 'Emerald (net8.0-windows10.0.22621)'
Before:
        SS = Ioc.Default.GetService<Helpers.Settings.SettingsSystem>();
        PropertyChanged += (_, e) =>
After:
        SS = Ioc.Default.GetService<SettingsSystem>();
        PropertyChanged += (_, e) =>
*/
        SS = Ioc.Default.GetService<Services.SettingsService>();
        PropertyChanged += (_, e) =>
        {
            //idk why I did this
            if (e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(ShowFontIcons))
            {
                InvokePropertyChanged(null);
            }
        };

        SS.Settings.App.Appearance.PropertyChanged += (_, e) =>
        {
            InvokePropertyChanged(null);
        };
    }
    public SquareNavigationViewItem(string name) : this()
    {
        Name = name;
    }
    public string Tag { get; set; }

    [ObservableProperty]
    private string _Name;

    [ObservableProperty]
    private string _FontIconGlyph;

    [ObservableProperty]
    private string _SolidFontIconGlyph;

    [ObservableProperty]
    private bool _IsSelected;

    [ObservableProperty]
    private bool _IsEnabled = true;

    [ObservableProperty]
    private string _Thumbnail;

    [ObservableProperty]
    private InfoBadge _InfoBadge;


    private bool ShowFontIcons => SS.Settings.App.Appearance.ShowFontIcons;

    //Using Converters is a pain in uno.
    public Visibility FontIconVisibility => ShowFontIcons && !IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SolidFontIconVisibility => ShowFontIcons && IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectionVisibility => IsSelected ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ImageVisibility => ShowFontIcons ? Visibility.Collapsed : Visibility.Visible;
    
}
