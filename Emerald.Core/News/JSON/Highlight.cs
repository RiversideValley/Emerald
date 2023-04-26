namespace Emerald.Core.News.JSON
{
    public class Highlight
    {
        public Image image { get; set; }

        public IconImage iconImage { get; set; }

        public List<string> platforms { get; set; }

        public List<object> entitlements { get; set; }

        public string title { get; set; }

        public string description { get; set; }

        public string readMoreLink { get; set; }

        public string until { get; set; }

        public string playGame { get; set; }
    }
}