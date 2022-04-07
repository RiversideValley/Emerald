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

namespace SDLauncher_UWP
{
    public class SettingsData
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
                        "\n      UUID          : This is the user ID to login with Microsoft,also can be get through the Microsoft Login." +
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
                    writer.WriteEndElement();
                    writer.WriteStartElement("App");
                    writer.WriteComment("\n    The theme of the app" +
                        "\n    values: \"Light\",\"Dark\",\"Default\"(System)" +
                        "\n    ");
                    writer.WriteStartElement("Theme");
                    if (vars.theme.ToString() == "")
                    {
                        writer.WriteAttributeString("value", "null");
                    }
                    else
                    {
                        writer.WriteAttributeString("value", vars.theme.ToString());
                    }
                    writer.WriteEndElement();
                    writer.WriteStartElement("Tips");
                    writer.WriteAttributeString("value", vars.ShowTips.ToString());
                    writer.WriteEndElement();
                    writer.WriteStartElement("AutoLogin");
                    writer.WriteAttributeString("value", vars.autoLog.ToString());
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
        public List<Account> Accounts = new List<Account>();
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
            string fixDiscord;
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
                    reader.ReadToFollowing("App");
                    reader.ReadToFollowing("Theme");
                    theme = reader.GetAttribute("value");
                    reader.ReadToFollowing("Tips");
                    tips = reader.GetAttribute("value");
                    reader.ReadToFollowing("AutoLogin");
                    autolog = reader.GetAttribute("value");
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

                    System.Xml.XmlNodeList list = doc.SelectNodes("//Settings/Minecraft/Accounts/Account");
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
                                string last = list[i].Attributes["LastAccessed"].Value;
                                if (last == "False")
                                {
                                    if(string.IsNullOrEmpty(avatarid))
                                    {
                                        Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, false));
                                    }
                                    else
                                    {
                                        Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, false,int.Parse(avatarid)));
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
                                        Accounts.Add(new Account(list[i].Attributes["Username"].Value, list[i].Attributes["Type"].Value, list[i].Attributes["AccessToken"].Value, list[i].Attributes["UUID"].Value, Accounts.Count + 1, true,int.Parse(avatarid)));
                                    }
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
                }
            }
            vars.LoadedRam = int.Parse(ram);
            if (theme == "Default")
            {
                vars.theme = ElementTheme.Default;
            }
            else if (theme == "Light")
            {
                vars.theme = ElementTheme.Light;
            }
            else if (theme == "Dark")
            {
                vars.theme = ElementTheme.Dark;
            }
            else
            {
                vars.theme = ElementTheme.Default;
            }
            if (Window.Current.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = (ElementTheme)vars.theme;
            }
            if (tips == "True")
            {
                vars.ShowTips = true;
            }
            else
            {
                vars.ShowTips = false;
            }
            if (hashcheck == "True")
            {
                vars.HashCheck = true;
            }
            else
            {
                vars.HashCheck = false;
            }
            if (assetscheck == "True")
            {
                vars.AssestsCheck = true;
            }
            else
            {
                vars.AssestsCheck = false;
            }
            if (autolog == "True")
            {
                vars.autoLog = true;
            }
            else
            {
                vars.autoLog = false;
            }
            if (fixDiscord == "True")
            {
                vars.IsFixedDiscord = true;
            }
            else
            {
                vars.IsFixedDiscord = false;
            }
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
    public class Account
    {
        List<string> PicList = new List<string>();
        public string UserName { get; set; }
        public string ProfilePicture { get; set; }
        public string Type { get; set; }
        public string TypeIconGlyph { get; set; }
        public string AccessToken { get; set; }
        public string UUID { get; set; }
        public int Count { get; set; }
        public int ProfileAvatarID { get; set; }
        public bool Last { get; set; }

        public Account(string username, string type, string accesstoken, string uuid, int count, bool last, int? pic = null)
        {
            PicList.Add("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/Pictures/steve.png");
            PicList.Add("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/Pictures/NoobSteve.png");
            PicList.Add("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/Pictures/alex.png");
            if (pic == null)
            {
                Random r = new Random();
                int index = r.Next(PicList.Count);
                ProfilePicture = PicList[index];
                ProfileAvatarID = index;
            }
            else
            {
                ProfilePicture = PicList[(int)pic];
                ProfileAvatarID = (int)pic;
            }
            UserName = username;
            Type = type;
            AccessToken = accesstoken;
            UUID = uuid;
            Count = count;
            Last = last;
            if(UUID != "null")
            {
                ProfilePicture = "https://minotar.net/avatar/" + UUID;
                ProfileAvatarID = 3;
            }
            TypeIconGlyph = Type == "Offline" ? "\xF384" : "\xEC05";
        }
    }
}
