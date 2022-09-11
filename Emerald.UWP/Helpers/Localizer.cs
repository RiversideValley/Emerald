using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace Emerald.UWP.Helpers
{
    public static class Localizer
    {

        private static ResourceLoader _resLoader = new ResourceLoader();

        public static string GetLocalizedString(string resourceKey)
        {
            var s = _resLoader.GetString(resourceKey);
            return string.IsNullOrEmpty(s) ? resourceKey : s;
        }
    }
}
