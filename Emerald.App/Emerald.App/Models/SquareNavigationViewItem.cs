using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Emerald.WinUI.Models
{
    public partial class SquareNavigationViewItem : Model
    {
        public SquareNavigationViewItem()
        {
        }

        public SquareNavigationViewItem(string name, bool isSelected = false, ImageSource image = null, InfoBadge infoBadge = null)
        {
            Name = name;
            IsSelected = isSelected;
            Thumbnail = image;
            InfoBadge = infoBadge;
        }


        [ObservableProperty]
        private string _Name;

        [ObservableProperty]
        private string _FontIconGlyph;

        [ObservableProperty]
        private bool _IsSelected;

        [ObservableProperty]
        private bool _ShowFontIcons;

        [ObservableProperty]
        private bool _IsEnabled = true;

        [ObservableProperty]
        private ImageSource _Thumbnail;

        [ObservableProperty]
        private InfoBadge _InfoBadge;
    }
}
