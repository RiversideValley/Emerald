using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.Models;

public partial class NavViewHeader : Model
{
    [ObservableProperty] private string _HeaderText;

    [ObservableProperty] private string _CustomButtonText;

    public Visibility CustomButtonVisibility => CustomButtonText == null ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty] private string _CustomContent;

    public Visibility CustomContentVisibility => CustomContent == null ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty] private Thickness _HeaderMargin;
}
