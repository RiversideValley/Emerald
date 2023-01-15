using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Emerald.WinUI.Models
{
    public class SquareNavigationViewItem : Model
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

        private string _name;
        public string Name { get => _name; set => Set(ref _name, value); }

        private string _FontIconGlyph;
        public string FontIconGlyph { get => _FontIconGlyph; set => Set(ref _FontIconGlyph, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

        private bool _ShowFontIcons;
        public bool ShowFontIcons { get => _ShowFontIcons; set => Set(ref _ShowFontIcons, value); }

        private bool _IsEnabled = true;
        public bool IsEnabled { get => _IsEnabled; set => Set(ref _IsEnabled, value); }

        private ImageSource _thumbnail;
        public ImageSource Thumbnail { get => _thumbnail; set => Set(ref _thumbnail, value); }

        private InfoBadge _infoBadge;
        public InfoBadge InfoBadge { get => _infoBadge; set => Set(ref _infoBadge, value); }
    }
}
