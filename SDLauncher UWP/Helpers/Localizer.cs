using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace SDLauncher_UWP.Helpers
{
    public static class Localizer
    {

        private static ResourceLoader _resLoader = new ResourceLoader();

        public static string GetLocalizedString(string resourceKey)
        {
            return _resLoader.GetString(resourceKey);
        }
    }
}
