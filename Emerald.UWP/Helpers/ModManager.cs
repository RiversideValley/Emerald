using Emerald.Core.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Emerald.UWP.Helpers
{

    public class StoreItem
    {
        public int ID { get; set; }
        public StoreManager.Type Type { get; set; }
        public string Name { get; set; }
        private List<string> sampleImages = new List<string>();
        public string Description { get; private set; }
        public async Task<string> BigDescription()
        {
            var mod = await Core.MainCore.Labrinth.GetProject(ProjectID,false);
            return await Util.DownloadText(mod.body_url);
        }
        public string ProjectID { get; set; }
        public string Author { get; set; }
        public int Followers { get; set; }
        public string FollowersString { get { return Followers.KiloFormat(); } }
        public int TotalDownloads { get; set; }
        public string TotalDownloadsString { get { return TotalDownloads.KiloFormat(); } }

        public string[] SupportedVers { get; set; }



        public List<BitmapImage> SampleImages
        {
            get
            {
                var b = new List<BitmapImage>();
                foreach (var item in sampleImages)
                {
                    b.Add(new BitmapImage(new Uri(item)));
                }
                return b;
            }
        }
        public BitmapImage Icon;
        public List<LabrinthResults.DownloadManager.DownloadLink> DownloadLinks;
        public StoreItem(object Item, int iD)
        {
            if(Item.GetType() == typeof(LabrinthResults.Hit))
            {
                var hit = (LabrinthResults.Hit)Item;
                this.Name = hit.title;
                this.Description = hit.description;
                this.Icon = new BitmapImage(new Uri(hit.icon_url));
                this.TotalDownloads = hit.downloads;
                this.SupportedVers = hit.versions;
                this.ProjectID = hit.project_id;
                this.sampleImages = new List<string>();
                this.Author = hit.author;
                this.Followers = hit.follows;
                foreach (var item in hit.gallery)
                {
                    sampleImages.Add(item);
                }
                DownloadLinks = new List<LabrinthResults.DownloadManager.DownloadLink>();
            }
            ID = iD;
        }
    }
    public class StoreManager
    {
        public enum Type
        {
            Mod,
            Shader
        }
        
        public class ModManager
        {
            public ModManager()
            {

            }
        }
        public Store store { get; set; }
        public static async Task<Store> GetStore()
        {
            try
            {
                string xml = await Util.DownloadText("https://raw.githubusercontent.com/SeaDevTeam/Emerald/main/Store/Store.xml");
                XmlSerializer x = new XmlSerializer(typeof(Store));
                Store s = (Store)x.Deserialize(new StringReader(xml));
                return s;
            }
            catch
            {
                return null;
            }
        }


        // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
        public partial class Store
        {

            private StoreShader[] shadersField;

            private StoreMod[] modsField;

            private string versionField;

            private string aPIVersionField;

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("Shader", IsNullable = false)]
            public StoreShader[] Shaders
            {
                get
                {
                    return this.shadersField;
                }
                set
                {
                    this.shadersField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("Mod", IsNullable = false)]
            public StoreMod[] Mods
            {
                get
                {
                    return this.modsField;
                }
                set
                {
                    this.modsField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Version
            {
                get
                {
                    return this.versionField;
                }
                set
                {
                    this.versionField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string APIVersion
            {
                get
                {
                    return this.aPIVersionField;
                }
                set
                {
                    this.aPIVersionField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreShader
        {

            private StoreShaderDownloadLink[] downloadLinksField;

            private StoreShaderSampleImage[] sampleImagesField;

            private StoreShaderFeature[] featuresField;

            private string nameField;

            private string iconField;

            private string descriptionField;

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("DownloadLink", IsNullable = false)]
            public StoreShaderDownloadLink[] DownloadLinks
            {
                get
                {
                    return this.downloadLinksField;
                }
                set
                {
                    this.downloadLinksField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("SampleImage", IsNullable = false)]
            public StoreShaderSampleImage[] SampleImages
            {
                get
                {
                    return this.sampleImagesField;
                }
                set
                {
                    this.sampleImagesField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("Feature", IsNullable = false)]
            public StoreShaderFeature[] Features
            {
                get
                {
                    return this.featuresField;
                }
                set
                {
                    this.featuresField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Name
            {
                get
                {
                    return this.nameField;
                }
                set
                {
                    this.nameField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Icon
            {
                get
                {
                    return this.iconField;
                }
                set
                {
                    this.iconField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Description
            {
                get
                {
                    return this.descriptionField;
                }
                set
                {
                    this.descriptionField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreShaderDownloadLink
        {

            private string urlField;

            private string supportedVerField;

            private string versionField;

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Url
            {
                get
                {
                    return this.urlField;
                }
                set
                {
                    this.urlField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string SupportedVer
            {
                get
                {
                    return this.supportedVerField;
                }
                set
                {
                    this.supportedVerField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Version
            {
                get
                {
                    return this.versionField;
                }
                set
                {
                    this.versionField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreShaderSampleImage
        {

            private string urlField;

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Url
            {
                get
                {
                    return this.urlField;
                }
                set
                {
                    this.urlField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreShaderFeature
        {

            private string nameField;

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Name
            {
                get
                {
                    return this.nameField;
                }
                set
                {
                    this.nameField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreMod
        {

            private StoreModDownloadLink[] downloadLinksField;

            private StoreModSampleImage[] sampleImagesField;

            private StoreModFeature[] featuresField;

            private string nameField;

            private string iconField;

            private string descriptionField;

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("DownloadLink", IsNullable = false)]
            public StoreModDownloadLink[] DownloadLinks
            {
                get
                {
                    return this.downloadLinksField;
                }
                set
                {
                    this.downloadLinksField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("SampleImage", IsNullable = false)]
            public StoreModSampleImage[] SampleImages
            {
                get
                {
                    return this.sampleImagesField;
                }
                set
                {
                    this.sampleImagesField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlArrayItemAttribute("Feature", IsNullable = false)]
            public StoreModFeature[] Features
            {
                get
                {
                    return this.featuresField;
                }
                set
                {
                    this.featuresField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Name
            {
                get
                {
                    return this.nameField;
                }
                set
                {
                    this.nameField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Icon
            {
                get
                {
                    return this.iconField;
                }
                set
                {
                    this.iconField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Description
            {
                get
                {
                    return this.descriptionField;
                }
                set
                {
                    this.descriptionField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreModDownloadLink
        {

            private string urlField;

            private string supportedVerField;

            private decimal versionField;

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Url
            {
                get
                {
                    return this.urlField;
                }
                set
                {
                    this.urlField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string SupportedVer
            {
                get
                {
                    return this.supportedVerField;
                }
                set
                {
                    this.supportedVerField = value;
                }
            }

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public decimal Version
            {
                get
                {
                    return this.versionField;
                }
                set
                {
                    this.versionField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreModSampleImage
        {

            private string urlField;

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Url
            {
                get
                {
                    return this.urlField;
                }
                set
                {
                    this.urlField = value;
                }
            }
        }

        /// <remarks/>
        [System.SerializableAttribute()]
        [System.ComponentModel.DesignerCategoryAttribute("code")]
        [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
        public partial class StoreModFeature
        {

            private string nameField;

            /// <remarks/>
            [System.Xml.Serialization.XmlAttributeAttribute()]
            public string Name
            {
                get
                {
                    return this.nameField;
                }
                set
                {
                    this.nameField = value;
                }
            }
        }



    }
}
