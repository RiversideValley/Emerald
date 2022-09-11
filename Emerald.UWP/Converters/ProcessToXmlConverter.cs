using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Storage;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Storage.Streams;
using System.IO;
using System.Xml;

namespace Emerald.UWP.Converters
{
    public class ProcessToXmlConverter
    {
        public async static Task Convert(Process Process,StorageFolder Destination,string FileName)
        {
            var inf = Process.StartInfo;
            var storagefile = await Destination.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
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
                    writer.WriteStartElement("Process");
                    writer.WriteStartElement("StartInfo");
                    writer.WriteAttributeString("Arguments", inf.Arguments);
                    writer.WriteAttributeString("FileName", inf.FileName);
                    writer.WriteAttributeString("GameLogs", vars.GameLogs.ToString());
                    writer.WriteAttributeString("WorkingDirectory", inf.WorkingDirectory);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.Flush();
                    await writer.FlushAsync();
                }
            }
        }
    }
}
