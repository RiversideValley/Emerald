using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers
{
    public static class Localizer
    {
        public static string ToLocalizedString(this string resourceKey)
        {
            try
            {
                var s = new ResourceLoader().GetString(resourceKey);
                return string.IsNullOrEmpty(s) ? resourceKey : s;
            }
            catch
            {
                return resourceKey;
            }
        }
    }
}
