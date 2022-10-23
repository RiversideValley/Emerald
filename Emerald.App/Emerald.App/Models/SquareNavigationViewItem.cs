using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Emerald.WinUI.Models
{
    internal class SquareNavigationViewItem : Model
    {

        public SquareNavigationViewItem(string name, bool isSelected = false, BitmapImage image = null, InfoBadge infoBadge = null)
        {
            Name = name;
            IsSelected = isSelected;
            Thumbnail = image;
            InfoBadge = infoBadge;
        }

        private string _name;
        public string Name { get => _name; set => Set(ref _name, value); }


        private string _glyphSecondary;
        public string GlyphSecondary { get => _glyphSecondary; set => Set(ref _glyphSecondary, value); }

        private bool _useOcticon;
        public bool UseOcticon { get => _useOcticon; set => Set(ref _useOcticon, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

        private BitmapImage _thumbnail;
        public BitmapImage Thumbnail { get => _thumbnail; set => Set(ref _thumbnail, value); }

        private InfoBadge _infoBadge;
        public InfoBadge InfoBadge { get => _infoBadge; set => Set(ref _infoBadge, value); }
    }
}
