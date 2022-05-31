using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Foundation;
using System.Xml;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using CmlLib.Core.Auth;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using SDLauncher_UWP.Helpers;

namespace SDLauncher_UWP.Helpers
{
    public class SettingsDataManager
    {

        public async Task CreateSettingsFile(bool? Exit)
        {
            var storagefile = await ApplicationData.Current.RoamingFolder.CreateFileAsync("settings.xml", CreationCollisionOption.ReplaceExisting);
            using (IRandomAccessStream writestream = await storagefile.OpenAsync(FileAccessMode.ReadWrite))
            {
                Stream s = writestream.AsStreamForWrite();
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Async = true;
                settings.NewLineOnAttributes = false;
                settings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(s, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Settings");
                    writer.WriteComment("\nThis is the settings of the SDLauncher uwp.Anyone can edit this.\n  For something true you must write \"True\" and something false you must write \"False\".");
                    writer.WriteStartElement("Minecraft");
                    writer.WriteComment("\n    The default maximum RAM of Minecraft in MegaByte.\n    The minimum RAM will be automatically decided by the Launcher.\n    ");
                    writer.WriteStartElement("RAM");
                    writer.WriteAttributeString("value", vars.CurrentRam.ToString());
                    writer.WriteEndElement();
                    writer.WriteStartElement("Accounts");
                    writer.WriteComment(
                        "\n      The accounts will be used on the Minecraft." +
                        "\n      Types         : \"Offline\",\"Microsoft\",\"null\"(not added\")." +
                        "\n      AcessToken    : This is the token to login with Microsoft, can be get through the Microsoft Login." +
                        "\n      UUID          : This is the UUID to login with Microsoft,also can be get through the Microsoft Login." +
                        "\n      AvatarID      : The avatar of your account. Values: \"0\",\"1\",\"2\",\"3\"(microsoft),\"\"(empty)" +
                        "\n      LastAccessed  : For autologin. Add this to only one account." +
                        "\n      ");
                    if (vars.Accounts != null)
                    {
                        foreach (var item in vars.Accounts)
                        {
                            writer.WriteStartElement("Account");
                            writer.WriteAttributeString("Type", item.Type);
                            writer.WriteAttributeString("Username", item.UserName);
                            writer.WriteAttributeString("AccessToken", item.AccessToken);
                            writer.WriteAttributeString("UUID", item.UUID);
                            if (item.Last)
                            {
                                writer.WriteAttributeString("LastAccessed", item.Last.ToString());
                            }
                            writer.WriteAttributeString("AvatarID", item.ProfileAvatarID.ToString());
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndElement();
                    writer.WriteStartElement("Downloader");
                    writer.WriteAttributeString("HashCheck", vars.HashCheck.ToString());
                    writer.WriteAttributeString("AssetsCheck", vars.AssestsCheck.ToString());
                    writer.WriteEndElement();
                    writer.WriteStartElement("JVM");
                    if (vars.JVMScreenWidth != 0 && vars.JVMScreenHeight != 0)
                    {
                        writer.WriteAttributeString("ScreenWidth", vars.JVMScreenWidth.ToString());
                        writer.WriteAttributeString("ScreenHeight", vars.JVMScreenHeight.ToString());
                    }
                    else
                    {
                        writer.WriteAttributeString("ScreenWidth", 0.ToString());
                        writer.WriteAttributeString("ScreenHeight", 0.ToString());
                    }
                    writer.WriteAttributeString("FullScreen", vars.FullScreen.ToString());
                    writer.WriteAttributeString("GameLogs", vars.GameLogs.ToString());
                    writer.WriteStartElement("Arguments");
                    if(vars.JVMArgs != null)
                    {
                        foreach (var item in vars.JVMArgs)
                        {
                            writer.WriteStartElement("Argument");
                            writer.WriteAttributeString("Content", item);
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteStartElement("App");
                    writer.WriteAttributeString("AutoClose", vars.AutoClose.ToString());
                    writer.WriteComment("\n    The theme and background of the app");
                    writer.WriteStartElement("Appearance");
                    writer.WriteAttributeString("CustomBackgroundImagePath", vars.BackgroundImagePath.ToString());
                    writer.WriteAttributeString("UseCustomBackgroundImage", vars.CustomBackground.ToString());
                    if (vars.Theme.ToString() == "")
                    {
                        writer.WriteAttributeString("Theme", "null");
                    }
                    else
                    {
                        writer.WriteAttributeString("Theme", vars.Theme.ToString());
                    }
                    writer.WriteEndElement();
                    writer.WriteStartElement("Tips");
                    writer.WriteAttributeString("value", vars.ShowTips.ToString());
                    writer.WriteEndElement();
                    writer.WriteStartElement("AutoLogin");
                    writer.WriteAttributeString("value", vars.autoLog.ToString());
                    writer.WriteEndElement();
                    writer.WriteStartElement("UseOldVersionsSeletor");
                    writer.WriteAttributeString("value", vars.UseOldVerSeletor.ToString());
                    writer.WriteEndElement();
                    writer.WriteStartElement("Discord");
                    writer.WriteAttributeString("IsPinned", vars.IsFixedDiscord.ToString());
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.Flush();
                    await writer.FlushAsync();
                }
            }

            if (vars.showXMLOnClose)
            {
                await Windows.System.Launcher.LaunchFileAsync(storagefile);
                vars.showXMLOnClose = false;
            }
            if (Exit == true)
            {
                Application.Current.Exit();
            }
        }
        public ObservableCollection<Account> Accounts = new ObservableCollection<Account>();
        public async Task LoadSettingsFile()
        {
            //var doc = await DocumentLoad().AsAsyncOperation();
            //var settings =  doc.GetElementById("settings");
            //var app = settings.GetElementsByTagName("value");
            //app.Contains(settings);
            var storagefile = await ApplicationData.Current.RoamingFolder.GetFileAsync("settings.xml");

            string theme;
            string tips;
            string ram;
            string hashcheck;
            string assetscheck;
            string autolog;
            string oldVer;
            string fixDiscord;
            string gamelogs;
            string jvmWidth;
            string jvmHeight;
            string jvmFullScreen;
            string isCustombg;
            string BGPath;
            string autoClose;
            using (IRandomAccessStream stream = await storagefile.OpenAsync(FileAccessMode.Read))
            {
                Stream s = stream.AsStreamForRead();
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.Async = true;
                using (XmlReader reader = XmlReader.Create(s, settings))
                {
                    await reader.ReadAsync();
                    reader.ReadStartElement("Settings");
                    reader.ReadToFollowing("Minecraft");
                    reader.ReadToFollowing("RAM");
                    ram = reader.GetAttribute("value");
                    reader.ReadToFollowing("Accounts");
                    reader.ReadToFollowing("Downloader");
                    hashcheck = reader.GetAttribute("HashCheck");
                    assetscheck = reader.GetAttribute("AssetsCheck");
                    reader.ReadToFollowing("JVM");
                    jvmWidth = reader.GetAttribute("ScreenWidth");
                    jvmHeight = reader.GetAttribute("ScreenHeight");
                    jvmFullScreen = reader.GetAttribute("FullScreen");
                    gamelogs = reader.GetAttribute("GameLogs");
                    reader.ReadToFollowing("App");
                    autoClose = reader.GetAttribute("AutoClose");
                    reader.ReadToFollowing("Appearance");
                    theme = reader.GetAttribute("Theme");
                    isCustombg = reader.GetAttribute("UseCustomBackgroundImage");
                    BGPath = reader.GetAttribute("CustomBackgroundImagePath");
                    reader.ReadToFollowing("Tips");
                    tips = reader.GetAttribute("value");
                    reader.ReadToFollowing("AutoLogin");
                    autolog = reader.GetAttribute("value");
                    reader.ReadToFollowing("UseOldVersionsSeletor");
                    oldVer = reader.GetAttribute("value");
                    reader.ReadToFollowing("Discord");
                    fixDiscord = reader.GetAttribute("IsPinned");

                }
                s = stream.AsStreamForRead();
                using (StreamReader streamReader = new StreamReader(s))
                {
                    string content;
                    content = await FileIO.ReadTextAsync(storagefile);
                    System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                    doc.LoadXml(content);

                    var list = doc.SelectNodes("//Settings/Minecraft/Accounts/Account");
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (list[i].Attributes["Type"].Value != "null")
                        {
                            string avatarid;
                            try
                            {
                                avatarid = list[i].Attributes["AvatarID"].Value;
                            }
                            catch
                            {
                                avatarid = null;
                            }
                            try
                            {
                                var lastv = list[i].Attributes["LastAccessed"];
                                if (lastv != null)
                                {
                                    string last = lastv.Value;
                                    if (last == "False")
                                    {
                                        if (string.IsNullOrEmpty(avatarid))
                                        {
                                            Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, false));
                                        }
                                        else
                                        {
                                            Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, false, int.Parse(avatarid)));
                                        }
                                    }
                                    else
                                    {
                                        if (string.IsNullOrEmpty(avatarid))
                                        {
                                            Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, true));
                                        }
                                        else
                                        {
                                            Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, true, int.Parse(avatarid)));
                                        }
                                    }
                                }
                                else
                                {
                                    throw new Exception();
                                }
                            }
                            catch
                            {

                                if (string.IsNullOrEmpty(avatarid))
                                {
                                    Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, false));
                                }
                                else
                                {
                                    Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, false, int.Parse(avatarid)));
                                }
                            }
                        }
                    }
                    vars.Accounts = Accounts;
                    vars.AccountsCount = Accounts.Count;
                    list = doc.SelectNodes("//Settings/Minecraft/JVM/Arguments/Argument");
                    var args = new List<string>();
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        args.Add(list[i].Attributes["Content"].Value);
                    }
                    vars.JVMArgs = args;
                }
            }
            int jvmwidth;
            int jvmheight;
            try
            {
                jvmheight = int.Parse(jvmHeight);
                jvmwidth = int.Parse(jvmWidth);
            }
            catch
            {
                jvmwidth = 0;
                jvmheight = 0;
            }
            vars.JVMScreenWidth = jvmwidth;
            vars.JVMScreenHeight = jvmheight;
            vars.BackgroundImagePath = BGPath;
            vars.LoadedRam = int.Parse(ram);
            if (theme == "Default")
            {
                vars.Theme = ElementTheme.Default;
            }
            else if (theme == "Light")
            {
                vars.Theme = ElementTheme.Light;
            }
            else
            {
                vars.Theme = theme == "Dark" ? (ElementTheme?)ElementTheme.Dark : (ElementTheme?)ElementTheme.Default;
            }
            if (Window.Current.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = (ElementTheme)vars.Theme;
            }
            vars.ShowTips = tips == "True";
            vars.HashCheck = hashcheck == "True";
            vars.UseOldVerSeletor = oldVer == "True";
            vars.AutoClose = autoClose == "True";
            vars.CustomBackground = isCustombg == "True";
            vars.GameLogs = gamelogs == "True";
            vars.AssestsCheck = assetscheck == "True";
            vars.autoLog = autolog == "True";
            vars.IsFixedDiscord = fixDiscord == "True";
            vars.FullScreen = jvmFullScreen == "True";
            if (vars.autoLog)
            {
                foreach (var item in vars.Accounts)
                {
                    if (item.Last)
                    {
                        if (item.Type != "null")
                        {
                            if (item.Type == "Offline")
                            {
                                if (item.UserName == "null")
                                {
                                    vars.UserName = "";
                                }
                                else
                                {
                                    vars.UserName = item.UserName;
                                    vars.session = MSession.GetOfflineSession(vars.UserName);
                                }
                            }
                            else
                            {
                                if (item.UserName != "null" && item.AccessToken != "null" && item.UUID != "null")
                                {
                                    vars.session = new MSession(item.UserName, item.AccessToken, item.UUID);
                                }
                            }
                        }
                        vars.CurrentAccountCount = item.Count;
                    }
                }

            }

        }
    }
}
