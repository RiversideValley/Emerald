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
            this.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IsSelected))
                {
                    InvokePropertyChanged(nameof(ShowFontIcons));
                    InvokePropertyChanged(nameof(ShowSolidFontIcons));
                }
            };
        }


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
        private ImageSource _Thumbnail;

        [ObservableProperty]
        private InfoBadge _InfoBadge;


        private bool _ShowFontIcons;
        public bool ShowFontIcons
        {
            get => _ShowFontIcons ? (IsSelected ? false : true) : false;
            set => Set(ref _ShowFontIcons, value);
        }
        public bool ShowSolidFontIcons => _ShowFontIcons ? (IsSelected ? true : false) : false;
        public bool ShowThumbnail => !_ShowFontIcons;
    }
}
