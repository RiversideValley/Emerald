namespace Emerald.Core.News.JSON
{
    public class Root
    {
        public int version { get; set; }

        public List<Entry> entries { get; set; }
    }
}