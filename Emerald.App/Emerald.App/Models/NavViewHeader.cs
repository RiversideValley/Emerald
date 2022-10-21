using Microsoft.UI.Xaml;

namespace Emerald.WinUI.Models
{
    public class NavViewHeader : Model
    {
        private string _HeaderText;
        public string HeaderText { get => _HeaderText; set => Set(ref _HeaderText, value); }

        private string _CustomButtonText;
        public string CustomButtonText { get => _CustomButtonText; set => Set(ref _CustomButtonText, value); }

        public Visibility CustomButtonVisibility { get => CustomButtonText == null ? Visibility.Collapsed : Visibility.Visible; }

        private Thickness _HeaderMargin;
        public Thickness HeaderMargin { get => _HeaderMargin; set => Set(ref _HeaderMargin, value); }
    }
}
