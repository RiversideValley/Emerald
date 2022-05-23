using SDLauncher_UWP.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDLauncher_UWP.Resources
{
    public static class Localized
    {
        public static string Welcome { get { return Localizer.GetLocalizedString("Welcome"); } }
        public static string GoodLuck { get { return Localizer.GetLocalizedString("GoodLuck"); } }
        public static string GoodEvening { get { return Localizer.GetLocalizedString("GoodEvening"); } }
        public static string GettingVers { get { return Localizer.GetLocalizedString("GettingVers"); } }
        public static string Ready { get { return Localizer.GetLocalizedString("Ready"); } }
        public static string RamFailed { get { return Localizer.GetLocalizedString("RamFailed"); } }
        public static string GoodMorning { get { return Localizer.GetLocalizedString("GoodMorning"); } }
        public static string BegLogIn { get { return Localizer.GetLocalizedString("BegLogIn"); } }
        public static string Error { get { return Localizer.GetLocalizedString("Error"); } }
        public static string BegVer { get { return Localizer.GetLocalizedString("BegVer"); } }
        public static string WrongRAM { get { return Localizer.GetLocalizedString("WrongRAM"); } }
        public static string NoNetwork { get { return Localizer.GetLocalizedString("NoNetwork"); } }
        public static string Win32Error { get { return Localizer.GetLocalizedString("Win32Error"); } }
        public static string GetVerFailed { get { return Localizer.GetLocalizedString("GetVerFailed"); } }
    }
}
