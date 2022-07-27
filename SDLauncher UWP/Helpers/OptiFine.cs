using SDLauncher.UWP.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Xml.Serialization;

namespace SDLauncher.UWP.Helpers
{
    public class OptiFineManager
    {
        public List<string> GetOptiFine()
        {
            return new List<string>();
        }
        public static class Deserializer
        {
			public static OptifineManager Deserialize(string OptiFineString)
			{
				XmlSerializer serializer = new XmlSerializer(typeof(OptifineManager));
				using (StringReader reader = new StringReader(OptiFineString))
                {
                    var test = (OptifineManager)serializer.Deserialize(reader);
					return test;
                }
            }
			[XmlRoot(ElementName = "Optifine")]
			public class Optifine
			{

				[XmlAttribute(AttributeName = "VersionName")]
				public string VersionName { get; set; }

				[XmlAttribute(AttributeName = "BasedOn")]
				public DateTime BasedOn { get; set; }

				[XmlAttribute(AttributeName = "DownloadUrl")]
				public string DownloadUrl { get; set; }
			}

			[XmlRoot(ElementName = "OptifineManager")]
			public class OptifineManager
			{

				[XmlElement(ElementName = "Optifine")]
				public List<Optifine> Optifine { get; set; }

				[XmlAttribute(AttributeName = "APIVersion")]
				public double APIVersion { get; set; }

				[XmlAttribute(AttributeName = "Version")]
				public double Version { get; set; }

				[XmlAttribute(AttributeName = "LibraryUrl")]
				public string LibraryUrl { get; set; }
			}

		}
	}

}
