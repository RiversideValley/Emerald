using Emerald.WinUI.Enums;
using Emerald.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Emerald.WinUI.UserControls
{
    [ContentProperty(Name = "Controls")]
    public sealed partial class Expander : UserControl
    {
        private readonly ExpanderViewModel VM = new();
        public Expander()
        {
            InitializeComponent();
            // this.Loaded += (_,_) =>
            //this.Content = new Helpers.CompositionControl { ContentTemplateSelector = ExpanderSelector, Content = VM };
        }
        public event RoutedEventHandler Click;
        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Title"/>
        /// </summary>
        public string Title
        {
            get => VM.Title;
            set => VM.Title = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Description"/>
        /// </summary>
        public string Description
        {
            get => VM.Description;
            set => VM.Description = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.ExpanderStyle"/>
        /// </summary>
        public ExpanderStyles ExpanderStyle
        {
            get => VM.ExpanderStyle;
            set => VM.ExpanderStyle = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Icon"/>
        /// </summary>
        public string Icon
        {
            get => VM.Icon;
            set => VM.Icon = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Controls"/>
        /// </summary>
        public object Controls
        {
            get => VM.Controls;
            set => VM.Controls = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.HeaderControls"/>
        /// </summary>
        public object HeaderControls
        {
            get => VM.HeaderControls;
            set => VM.HeaderControls = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.IsExpanded"/>
        /// </summary>
        public bool IsExpanded
        {
            get => VM.IsExpanded;
            set => VM.IsExpanded = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.MinDetailsWidth"/>
        /// </summary>
        public int MinDetailsWidth
        {
            get => VM.MinDetailsWidth;
            set => VM.MinDetailsWidth = value;
        }
        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this, e);
        }
    }
}