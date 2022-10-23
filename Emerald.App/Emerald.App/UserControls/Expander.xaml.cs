using Emerald.WinUI.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using System.ComponentModel;

namespace Emerald.WinUI.UserControls
{
    [ContentProperty(Name = "Controls")]
    public sealed partial class Expander : UserControl, INotifyPropertyChanged
    {
        public Expander()
        {
            this.InitializeComponent();
        }
        private ExpanderStyles _expanderStyle;
        /// <summary>
        /// Gets or sets the style for the expander.
        /// </summary>
        public ExpanderStyles ExpanderStyle
        {
            get => _expanderStyle;
            set => Set(ref _expanderStyle, value);
        }

        private string _title;
        /// <summary>
        /// Gets or sets the title for the expander.
        /// </summary>
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _description;
        /// <summary>
        /// Gets or sets the description for the expander.
        /// </summary>
        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        private string _icon;
        /// <summary>
        /// Gets or sets the icon for the expander as a glyph.
        /// </summary>
        public string Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        private object _controls;
        /// <summary>
        /// Gets or sets the content for the expander. This is
        /// displayed to the left of most expander styles, but
        /// the default one uses <see cref="HeaderControls"/>
        /// for that purpose.
        /// </summary>
        public object Controls
        {
            get => _controls;
            set => Set(ref _controls, value);
        }

        private object _headerControls;
        /// <summary>
        /// Gets or sets the header content for the default
        /// expander.
        /// </summary>
        public object HeaderControls
        {
            get => _headerControls;
            set => Set(ref _headerControls, value);
        }
        public void Set<T>(ref T obj, T value, string name = null)
        {
            obj = value;
            PropertyChanged?.Invoke(this, new(null));
        }
        public event RoutedEventHandler Click;
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this, e);
        }
    }
}
