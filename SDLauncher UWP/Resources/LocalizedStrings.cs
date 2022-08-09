using SDLauncher.UWP.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDLauncher.UWP.Resources
{
    public static class Localized
    {
        public static string Welcome => Localizer.GetLocalizedString("Welcome");
        public static string GoodLuck => Localizer.GetLocalizedString("GoodLuck");
        public static string GoodEvening => Localizer.GetLocalizedString("GoodEvening");
        public static string GettingVers => Localizer.GetLocalizedString("GettingVers");
        public static string Ready => Localizer.GetLocalizedString("Ready");
        public static string RamFailed => Localizer.GetLocalizedString("RamFailed");
        public static string GoodMorning => Localizer.GetLocalizedString("GoodMorning");
        public static string BegLogIn => Localizer.GetLocalizedString("BegLogIn");
        public static string Error => Localizer.GetLocalizedString("Error");
        public static string BegVer => Localizer.GetLocalizedString("BegVer");
        public static string WrongRAM => Localizer.GetLocalizedString("WrongRAM");
        public static string NoNetwork => Localizer.GetLocalizedString("NoNetwork");
        public static string Win32Error => Localizer.GetLocalizedString("Win32Error");
        public static string GetVerFailed => Localizer.GetLocalizedString("GetVerFailed");
        public static string RefreshVerFailed => Localizer.GetLocalizedString("RefreshVerFailed");
        public static string UnexpectedRestart => Localizer.GetLocalizedString("UnexpectedRestart");
    }
}
