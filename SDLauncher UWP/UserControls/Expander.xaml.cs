using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SDLauncher_UWP.UserControls
{
    public sealed partial class Expander : UserControl
    {
        public Expander()
        {
            this.InitializeComponent();
        }



        public TransitionCollection ControlsTransition
        {
            get { return (TransitionCollection)GetValue(ControlsTransitionProperty); }
            set { SetValue(ControlsTransitionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ControlsTransition.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ControlsTransitionProperty =
            DependencyProperty.Register("ControlsTransition", typeof(TransitionCollection), typeof(Expander), null);




        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsExpanded.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register("IsExpanded", typeof(bool), typeof(Expander), null);


        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Icon.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(string), typeof(Expander), null);


        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Title.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(Expander), null);



        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Description.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register("Description", typeof(string), typeof(Expander), null);



        public object HeaderControls
        {
            get { return (object)GetValue(HeaderControlsProperty); }
            set { SetValue(HeaderControlsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HeaderControls.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HeaderControlsProperty =
            DependencyProperty.Register("HeaderControls", typeof(object), typeof(Expander), null);



        public object Controls
        {
            get { return (object)GetValue(ControlsProperty); }
            set { SetValue(ControlsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Controls.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ControlsProperty =
            DependencyProperty.Register("Controls", typeof(object), typeof(Expander), null);

        private void container_Loaded(object sender, RoutedEventArgs e)
        {
            if (ControlsTransition != null)
            {
                container.ChildrenTransitions = ControlsTransition;
            }
        }
    }
}
