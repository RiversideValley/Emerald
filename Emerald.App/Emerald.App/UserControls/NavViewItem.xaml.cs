using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.UserControls
{
    public sealed partial class NavViewItem : NavigationViewItem, INotifyPropertyChanged
    {
        public NavViewItem()
        {
            InitializeComponent();
        }
        private string _IconGlyph;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string IconGlyph
        {
            get { return string.IsNullOrEmpty(_IconGlyph) ? "\xe8a5" : _IconGlyph; }
            set
            {
                _IconGlyph = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconGlyph)));
            }
        }
    }
}
