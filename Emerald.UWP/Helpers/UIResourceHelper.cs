using Emerald.UWP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.UWP.Helpers
{
    public static class UIResourceHelper
    {
        public static ResourceStyle? CurrentStyle { get; set; } = null;
        public static void SetResource(ResourceStyle style)
        {
            switch (style)
            {
                case ResourceStyle.Mica:
                    if(CurrentStyle == ResourceStyle.Acrylic)
                    {
                        App.Current.Resources.MergedDictionaries.Add(App.MicaStyle);
                        App.Current.Resources.MergedDictionaries.Remove(App.AcrylicStyle);
                    }else if(CurrentStyle == null)
                    {
                        App.Current.Resources.MergedDictionaries.Add(App.MicaStyle);
                    }
                    break;
                case ResourceStyle.Acrylic:
                    if (CurrentStyle == ResourceStyle.Mica)
                    {
                        App.Current.Resources.MergedDictionaries.Add(App.AcrylicStyle);
                        App.Current.Resources.MergedDictionaries.Remove(App.MicaStyle);
                    }
                    else if (CurrentStyle == null)
                    {
                        App.Current.Resources.MergedDictionaries.Add(App.AcrylicStyle);
                    }
                    break;
            }
            CurrentStyle = style;
        }
    }
    public enum ResourceStyle
    {
        Mica,
        Acrylic
    }
}
