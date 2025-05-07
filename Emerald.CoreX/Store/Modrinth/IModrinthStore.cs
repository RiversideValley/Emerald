using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emerald.CoreX.Store.Modrinth.JSON;

namespace Emerald.CoreX.Store.Modrinth;

public interface IModrinthStore
{
    /// <summary>
    /// Searches for items in the Modrinth store based on the provided query and options.
    /// </summary>
    /// <param name="query">The search query string used to find items.</param>
    /// <param name="limit">The maximum number of results to return. Default is 15.</param>
    /// <param name="sortOptions">The sorting option to apply to the results, such as relevance or downloads.</param>
    /// <param name="categories">An optional array of category names to filter the results by specific categories.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="SearchResult"/> object representing the search results, or null if an error occurred.</returns>
    public Task<SearchResult?> SearchAsync(string query, int limit = 15,
        SearchSortOptions sortOptions = SearchSortOptions.Relevance, string[]? categories = null);

    /// <summary>
    /// Loads the available categories from the Modrinth API for the specified project type.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task LoadCategoriesAsync();

    /// <summary>
    /// Retrieves detailed information about a specific item from the Modrinth store.
    /// </summary>
    /// <param name="id">The unique identifier of the item.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="StoreItem"/> object with the item's details, or null if an error occurred.</returns>
    public Task<StoreItem?> GetItemAsync(string id);

    /// <summary>
    /// Retrieves all versions of a specific item from the Modrinth store.
    /// </summary>
    /// <param name="id">The unique identifier of the item to retrieve versions for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="ItemVersion"/> objects associated with the item, or null if an error occurred.</returns>
    public Task<List<ItemVersion>?> GetVersionsAsync(string id);

    /// <summary>
    /// Downloads a specific file for an item from the Modrinth store.
    /// </summary>
    /// <param name="file">An instance of <see cref="ItemFile"/> containing details about the file to be downloaded.</param>
    /// <param name="projectType">The category or type of the project, such as "mods" or "resourcepacks".</param>
    /// <param name="progress">Optional. A progress reporter of type <see cref="IProgress{double}"/> to report the download progress.</param>
    /// <param name="cancellationToken">Optional. A token to monitor for cancellation requests while the operation is in progress.</param>
    /// <returns>A task that represents the asynchronous operation of downloading the item.</returns>
    public Task DownloadItemAsync(ItemFile file, string projectType, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    public Category[] Categories { get; }
}
