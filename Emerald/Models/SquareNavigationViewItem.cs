using System.ComponentModel;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.Models;

public partial class SquareNavigationViewItem : Model
{
    private readonly Services.SettingsService SS;
    public SquareNavigationViewItem()
    {
        SS = ServiceLocator.Current.GetInstance<Services.SettingsService>();
        PropertyChanged += (_, e) =>
        {
            //idk why I did this
            if (e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(ShowFontIcons))
            {
                InvokePropertyChanged();
            }
        };

        SS.Settings.App.Appearance.PropertyChanged += (_, e) =>
        {
            InvokePropertyChanged();
        };
    }
    public SquareNavigationViewItem(string name) : this()
    {
        Name = name;
    }
    public string? Tag { get; set; }

    [ObservableProperty]
    private string? _Name;

    [ObservableProperty]
    private string? _FontIconGlyph;

    [ObservableProperty]
    private string? _SolidFontIconGlyph;

    [ObservableProperty]
    private bool _IsSelected;

    [ObservableProperty]
    private bool _IsEnabled = true;

    [ObservableProperty]
    private string? _Thumbnail;

    [ObservableProperty]
    private InfoBadge? _InfoBadge;


    private bool ShowFontIcons => SS.Settings.App.Appearance.ShowFontIcons;

    //Using Converters is a pain in uno.
    public Visibility FontIconVisibility => ShowFontIcons && !IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SolidFontIconVisibility => ShowFontIcons && IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectionVisibility => IsSelected ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ImageVisibility => ShowFontIcons ? Visibility.Collapsed : Visibility.Visible;
    
}
