using System.ComponentModel;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Media;

namespace Emerald.Models;

public partial class SquareNavigationViewItem : Model
{
    private readonly Services.SettingsService SS;
    public SquareNavigationViewItem()
    {
        SS = Ioc.Default.GetService<Services.SettingsService>();
        PropertyChanged += (_, e) =>
        {
            //idk why I did this
            if (e.PropertyName == nameof(IsSelected) || e.PropertyName == nameof(ShowFontIcons) || e.PropertyName == nameof(Thumbnail) || e.PropertyName == nameof(Avatar))
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
    private ImageSource? _avatar;

    [ObservableProperty]
    private InfoBadge _InfoBadge;


    private bool ShowFontIcons => SS.Settings.App.Appearance.ShowFontIcons;
    private bool HasAvatar => Avatar is not null;
    private bool UseFontIcons => !HasAvatar && (ShowFontIcons || string.IsNullOrWhiteSpace(Thumbnail));

    //Using Converters is a pain in uno.
    public Visibility FontIconVisibility => UseFontIcons && !IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SolidFontIconVisibility => UseFontIcons && IsSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectionVisibility => IsSelected ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ImageVisibility => !HasAvatar && !UseFontIcons ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AvatarVisibility => HasAvatar ? Visibility.Visible : Visibility.Collapsed;
    
}
