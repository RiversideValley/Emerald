using Emerald.WinUI.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Emerald.WinUI.Converters
{
    #region TaskViewTemplateSelector
    public class TaskViewTemplateSelector : DataTemplateSelector
    {

        public DataTemplate String { get; set; }
        public DataTemplate Progress { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return ((Models.ITask)item is Models.StringTask) ? String : Progress;
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return ((Models.ITask)item is Models.StringTask) ? String : Progress;
        }
    }
    #endregion
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
}
