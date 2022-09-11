using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Emerald.UWP.Helpers
{
    public class MenuItemsCreator
    {
        public enum MCType
        {
            Vanilla,
            Fabric
        }
        public event EventHandler<ItemInvokedArgs> ItemInvoked = delegate { };
        public  MenuFlyout Flyout;
        public MenuFlyout CreateVersions()
        {
            Flyout = new MenuFlyout();
            AddItem("1.19");
            AddItem("1.18");
            AddItem("1.17");
            AddItem("1.16");
            AddItem("1.15");
            AddItem("1.12");
            AddItem("1.11");
            AddItem("1.10");
            AddItem("1.9");
            AddItem("1.8");
            AddItem("1.7");
            AddItem("1.6");
            AddItem("1.5");
            AddItem("1.4");
            AddItem("1.3");
            AddItem("1.2");
            AddItem("1.1");
            return Flyout;
        }
        public void AddItem(string ver)
        {
            var m = GetFromStrings(ver);
            if (m != null) 
            {
                Flyout.Items.Add(m);
            }
        }
        public MenuFlyoutItemBase GetFromStrings(string ver)
        {
            if (Core.MainCore.Launcher.MCVerNames.Contains(ver))
            {
                var subVers = Core.MainCore.Launcher.GetSubVersions(ver);
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
            else
            {
                return null;
            }
        }
        public MenuFlyoutItemBase ReturnMCWithFabric(string ver)
        {
            string fabricVer = Core.MainCore.Launcher.SearchFabric(ver);
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
