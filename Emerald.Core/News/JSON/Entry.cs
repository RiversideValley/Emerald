using System.Text.Json.Serialization;

namespace Emerald.Core.News.JSON
{

    public class Entry
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("tag")]
        public string Tag { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("playPageImage")]
        public PlayPageImage PlayPageImage { get; set; }

        [JsonPropertyName("newsPageImage")]
        public NewsPageImage NewsPageImage { get; set; }

        [JsonPropertyName("readMoreLink")]
        public string ReadMoreLink { get; set; }

        [JsonPropertyName("cardBorder")]
        public bool CardBorder { get; set; }

        [JsonPropertyName("newsType")]
        public List<string> NewsType { get; set; }

        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("highlight")]
        public Highlight Highlight { get; set; }

        public string ImageUri
            => "https://launchercontent.mojang.com" + NewsPageImage.url;

        public DateTime DateReadable
        {
            get
            {
                var d = Date.Split('-').Select(int.Parse).ToArray();
                return new DateTime(d[0], d[1], d[2]);
            }
        }
    }
}