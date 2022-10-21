using Emerald.WinUI.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

//Copied from RiseMP

#region ViewModels
namespace Emerald.WinUI.ViewModels
{/// <summary>
 /// Base ViewModel implementation, wraps a <typeparamref name="Type"/>
 /// object for the Model-View-ViewModel pattern and contains methods
 /// to handle property changes.
 /// </summary>
 /// <typeparam name="Type">Type of the underlying model.</typeparam>
    public abstract class ViewModel<Type> : ViewModel, IEquatable<Type>
    {
        private Type _model;
        /// <summary>
        /// Gets or sets the underlying <see cref="Type"/> object.
        /// </summary>
        public Type Model
        {
            get => _model;
            set
            {
                if (_model == null || !_model.Equals(value))
                {
                    _model = value;

                    // Raise the PropertyChanged event for all properties.
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        public bool Equals(Type other)
        {
            return other.Equals(Model);
        }
    }

    /// <summary>
    /// Base ViewModel implementation, contains methods to
    /// handle property changes.
    /// </summary>
    public abstract class ViewModel : INotifyPropertyChanged
    {
        private readonly Dictionary<PropertyChangedEventHandler, SynchronizationContext> PropertyChangedEvents =
            new();

        /// <summary>
        /// Whether or not to send property change notifications.
        /// </summary>
        protected bool NotifyPropertyChanges = true;

        /// <summary> 
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                PropertyChangedEvents.Add(value, SynchronizationContext.Current);
            }
            remove
            {
                PropertyChangedEvents.Remove(value);
            }
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners. This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (!NotifyPropertyChanges)
            {
                return;
            }

            PropertyChangedEventArgs args = new(propertyName);
            foreach (KeyValuePair<PropertyChangedEventHandler, SynchronizationContext> @event in PropertyChangedEvents)
            {
                if (@event.Value == null)
                {
                    @event.Key.Invoke(this, args);
                }
                else
                {
                    @event.Value.Post(s => @event.Key.Invoke(s, args), this);
                }
            }
        }

        /// <summary>
        /// Checks if a property already matches a desired value. Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners. This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        protected bool Set<T>(ref T storage, T value,
            [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;

            if (NotifyPropertyChanges)
            {
                OnPropertyChanged(propertyName);
            }

            return true;
        }
    }
    public class ExpanderViewModel : ViewModel
    {
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
    }
}
#endregion

namespace Emerald.WinUI.UserControls
{
    [ContentProperty(Name = "Controls")]
    public sealed partial class Expander : UserControl
    {
        private readonly ViewModels.ExpanderViewModel ViewModel = new();
        public Expander()
        {
            this.InitializeComponent();
        }
        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Title"/>
        /// </summary>
        public string Title
        {
            get => ViewModel.Title;
            set => ViewModel.Title = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Description"/>
        /// </summary>
        public string Description
        {
            get => ViewModel.Description;
            set => ViewModel.Description = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.ExpanderStyle"/>
        /// </summary>
        public ExpanderStyles ExpanderStyle
        {
            get => ViewModel.ExpanderStyle;
            set => ViewModel.ExpanderStyle = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Icon"/>
        /// </summary>
        public string Icon
        {
            get => ViewModel.Icon;
            set => ViewModel.Icon = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.Controls"/>
        /// </summary>
        public object Controls
        {
            get => ViewModel.Controls;
            set => ViewModel.Controls = value;
        }

        /// <summary>
        /// <inheritdoc cref="ExpanderViewModel.HeaderControls"/>
        /// </summary>
        public object HeaderControls
        {
            get => ViewModel.HeaderControls;
            set => ViewModel.HeaderControls = value;
        }

        public event RoutedEventHandler Click;
        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this, e);
        }
    }
    #region ExpanderTemplateSelector
    public class ExpanderTemplateSelector : DataTemplateSelector
    {
        public ExpanderStyles Style { get; set; }

        public DataTemplate Default { get; set; }
        public DataTemplate Static { get; set; }
        public DataTemplate Button { get; set; }
        public DataTemplate Transparent { get; set; }
        public DataTemplate Disabled { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            switch (Style)
            {
                case ExpanderStyles.Static:
                    return Static;

                case ExpanderStyles.Button:
                    return Button;

                case ExpanderStyles.Transparent:
                    return Transparent;

                case ExpanderStyles.Disabled:
                    return Disabled;

                default:
                    return Default;
            }
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            switch (Style)
            {
                case ExpanderStyles.Static:
                    return Static;

                case ExpanderStyles.Button:
                    return Button;

                case ExpanderStyles.Transparent:
                    return Transparent;

                case ExpanderStyles.Disabled:
                    return Disabled;

                default:
                    return Default;
            }
        }
    }
    // https://social.msdn.microsoft.com/Forums/windowsapps/en-US/b2e0f948-df35-49da-a70f-1892205b8570/contenttemplateselector-datatemplateselectorselecttemplatecore-item-parameter-is-always-null?forum=winappswithcsharp
    #endregion
    /// <summary>
    /// A version of the ContentControl that works with the ContentTemplateSelector.
    /// </summary>
    public class CompositionControl : ContentControl
    {
        /// <summary>
        /// Invoked when the value of the Content property changes. 
        /// </summary>
        /// <param name="oldContent">The old value of the Content property.</param>
        /// <param name="newContent">The new value of the Content property.</param>
        protected override void OnContentChanged(object oldContent, object newContent)
        {
            // There is a bug in the standard content control that trashes the value passed into the SelectTemplateCore method.  This is a
            // work-around that allows the same basic structure and can hopefully be replaced when the bug is fixed.  Basically take the new content
            // and figure out what template should be used with it based on the structure of the template selector.
            if (ContentTemplateSelector is DataTemplateSelector dataTemplateSelector)
            {
                ContentTemplate = dataTemplateSelector.SelectTemplate(newContent, null);
            }

            // Allow the base class to handle the rest of the call.
            base.OnContentChanged(oldContent, newContent);
        }
    }
}
