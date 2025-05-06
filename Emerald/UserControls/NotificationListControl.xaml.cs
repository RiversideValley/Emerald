using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Emerald.CoreX.Notifications;
using CommunityToolkit.Mvvm.DependencyInjection;
// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Emerald.UserControls;
public sealed partial class NotificationListControl : UserControl
{
    public NotificationListControl()
    {
        this.InitializeComponent();
        DataContext = Ioc.Default.GetService<NotificationListViewModel>();
    }
}

public class NotificationTemplateSelector : DataTemplateSelector
{
    public DataTemplate ProgressTemplate { get; set; }
    public DataTemplate DefaultTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (item is NotificationViewModel vm)
            return vm.Type == NotificationType.Progress ? ProgressTemplate : DefaultTemplate;

        return base.SelectTemplateCore(item);
    }

}
