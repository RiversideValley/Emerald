using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.UserControls
{
    public sealed partial class NavViewItem : NavigationViewItem,   INotifyPropertyChanged
    {
        public NavViewItem()
        {
            this.InitializeComponent();
        }
        private string _IconGlyph;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string IconGlyph
        {
            get { return string.IsNullOrEmpty(_IconGlyph) ? "\xe8a5" : _IconGlyph; }
            set
            {
                _IconGlyph = value;
                this.PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(IconGlyph)));
            }
        }
    }
}
