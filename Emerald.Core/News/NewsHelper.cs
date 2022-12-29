using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.Core.News
{
    public class NewsHelper
    {
        public NewsHelper() { }
        public async Task<List<JSON.Entry>> GetEntries()
        {
            string response;
            using (var wc = new HttpClient())
            {
                var url = "https://launchercontent.mojang.com/news.json";
                response = await wc.GetStringAsync(url)
                    .ConfigureAwait(false);
            }
            var json = JsonConvert.DeserializeObject<JSON.Root>(response);
            return json?.entries != null ? json.entries.Where(x => x.Category == "Minecraft: Java Edition").ToList() : new();
        }
    }
}

