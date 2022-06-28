using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SDLauncher_UWP.Helpers
{
    public class MenuItemsCreator
    {
        public enum MCType
        {
            Vanilla,
            Fabric
        }
        public event EventHandler<ItemInvokedArgs> ItemInvoked = delegate { };
        public MenuFlyout CreateVersions()
        {
            var f = new MenuFlyout();
            f.Items.Add(GetFromStrings("1.19"));
            f.Items.Add(GetFromStrings("1.18"));
            f.Items.Add(GetFromStrings("1.17"));
            f.Items.Add(GetFromStrings("1.16"));
            f.Items.Add(GetFromStrings("1.15"));
            f.Items.Add(GetFromStrings("1.12"));
            f.Items.Add(GetFromStrings("1.11"));
            f.Items.Add(GetFromStrings("1.10"));
            f.Items.Add(GetFromStrings("1.9"));
            f.Items.Add(GetFromStrings("1.8"));
            f.Items.Add(GetFromStrings("1.7"));
            f.Items.Add(GetFromStrings("1.6"));
            f.Items.Add(GetFromStrings("1.5"));
            f.Items.Add(GetFromStrings("1.4"));
            f.Items.Add(GetFromStrings("1.3"));
            f.Items.Add(GetFromStrings("1.2"));
            f.Items.Add(GetFromStrings("1.1"));
            return f;
        }
        public MenuFlyoutItemBase GetFromStrings(string ver)
        {
            var subVers = vars.Launcher.GetSubVersions(ver);
            if (subVers.Count() > 1)
            {
                MenuFlyoutSubItem f = new MenuFlyoutSubItem();
                f.Text = ver;
                foreach (var item in subVers)
                {
                    f.Items.Add(ReturnMCWithFabric(item));
                }
                return f;
            }
            else
            {
                return CreateItem(ver, "vaniila-" + ver);
            }
        }
        public MenuFlyoutItemBase ReturnMCWithFabric(string ver)
        {
            string fabricVer = vars.Launcher.SearchFabric(ver);
            if (string.IsNullOrEmpty(fabricVer))
            {
                return CreateItem(ver, "vaniila-" + ver);
            }
            else
            {
                var i = new MenuFlyoutSubItem();
                i.Text = ver;
                i.Items.Add(CreateItem(ver, "vaniila-" + ver));
                i.Items.Add(CreateItem("Fabric " + ver, "fabricMC-" + fabricVer));
                return i;
            }
        }
        public MenuFlyoutItem CreateItem(string text, string tag)
        {
            var i = new MenuFlyoutItem { Text = text, Tag = tag };
            i.Click += MCVerClick;
            return i;
        }

        private void MCVerClick(object sender, RoutedEventArgs e)
        {
            if(sender is MenuFlyoutItem mit)
            {
                if (mit.Tag.ToString().StartsWith("fabricMC-"))
                {
                    this.ItemInvoked(this, new ItemInvokedArgs(mit.Tag.ToString().Replace("fabricMC-", "").ToString(), mit.Text, MCType.Fabric));
                }
                else if(mit.Tag.ToString().StartsWith("vanilla-"))
                {
                    this.ItemInvoked(this, new ItemInvokedArgs(mit.Tag.ToString().Replace("vanilla-", "").ToString(), mit.Text, MCType.Vanilla));
                }
            }
        }
        public class ItemInvokedArgs : EventArgs
        {
           public string Ver { get; private set; }
           public string DisplayVer { get; private set; }
           public MCType MCType { get; private set; }
            public ItemInvokedArgs(string ver, string displayVer, MCType mCType)
            {
                Ver = ver;
                DisplayVer = displayVer;
                MCType = mCType;
            }
        }
    }
}
