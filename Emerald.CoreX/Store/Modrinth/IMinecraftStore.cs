using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emerald.CoreX.Store.Modrinth.JSON;

namespace Emerald.CoreX.Store.Modrinth;

public interface IModrinthStore
{
    Task<SearchResult?> SearchAsync(string query, int limit = 15,
        SearchSortOptions sortOptions = SearchSortOptions.Relevance, string[]? categories = null);

    Task<StoreItem?> GetItemAsync(string id);
    Task<List<ItemVersion>?> GetVersionsAsync(string id);
    Task DownloadItemAsync(ItemFile file, string projectType);
}
