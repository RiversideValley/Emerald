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

        private static ResourceLoader _resLoader = new ResourceLoader();

        public static string ToLocalizedString(this string resourceKey)
        {
            var s = _resLoader.GetString(resourceKey);
            return string.IsNullOrEmpty(s) ? resourceKey : s;
        }
    }
}
