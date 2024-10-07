using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emerald.CoreX.Store.Modrinth.JSON;

namespace Emerald.CoreX.Store.Modrinth;

public interface IModrinthStore
{
    public Task<SearchResult?> SearchAsync(string query, int limit = 15,
        SearchSortOptions sortOptions = SearchSortOptions.Relevance, string[]? categories = null);
    public Task LoadCategoriesAsync();
    public Task<StoreItem?> GetItemAsync(string id);
    public Task<List<ItemVersion>?> GetVersionsAsync(string id);
    public Task DownloadItemAsync(ItemFile file, string projectType, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    public Category[] Categories { get; }
}
