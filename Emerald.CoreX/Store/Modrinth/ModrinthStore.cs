using CmlLib.Core;
using System.Text.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Emerald.CoreX.Store.Modrinth.JSON;

namespace Emerald.CoreX.Store.Modrinth;

public abstract class ModrinthStore : IMinecraftStore
{
    protected readonly RestClient _client;
    public MinecraftPath MCPath { get; }
    protected readonly ILogger _logger;
    protected readonly string _projectType;
    public Category[] Categories { get; private set; } = [];

    /// <summary>
    /// Initializes a new instance of the ModrinthStore class.
    /// </summary>
    /// <param name="path">The Minecraft path.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="projectType">The type of project (e.g., mod, plugin, resourcepack).</param>
    protected ModrinthStore(MinecraftPath path, ILogger logger, string projectType)
    {
        _client = new RestClient("https://api.modrinth.com/v2/");
        _client.AddDefaultHeader("Accept", "application/json");
        MCPath = path;
        _logger = logger;
        _projectType = projectType;
    }


    /// <summary>
    /// Loads categories for the specified project type from the Modrinth API.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadCategoriesAsync()
    {
        var request = new RestRequest("tag/category");

        try
        {
            var response = await _client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                var all = JsonSerializer.Deserialize<List<Category>>(response.Content);

                var _categories = all
                    .Where(i => i.header == "categories"
                                && i.project_type == _projectType
                                && !string.IsNullOrWhiteSpace(i.icon)
                                && !string.IsNullOrWhiteSpace(i.name))
                    .ToList();
                Categories = _categories.ToArray();
                _logger.LogInformation($"Loaded {_categories.Count} {_projectType} categories.");
            }
            else
            {
                _logger.LogError($"Failed to load {_projectType} categories. Status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while loading {_projectType} categories.");
        }
    }

    /// <summary>
    /// Searches for items in the Modrinth store based on the provided query and options.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="limit">The maximum number of results to return (default is 15).</param>
    /// <param name="sortOptions">The sorting options for the search results.</param>
    /// <param name="categories">An optional array of category names to filter the results.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the search results or null if an error occurred.</returns>
    public virtual async Task<SearchResult?> SearchAsync(string query, int limit = 15,
        SearchSortOptions sortOptions = SearchSortOptions.Relevance, string[]? categories = null)
    {
        _logger.LogInformation($"Searching store for {_projectType}s with query: {query}");

        try
        {
            // Prepare the facets parameter correctly
            string facets = "[[\"project_type:" + _projectType + "\"]";
            if (categories != null && categories.Length != 0)
            {
                var categoryFacets = categories.Select(cat => $"\"categories:{cat}\"");
                facets += ",[" + string.Join(",", categoryFacets) + "]";
            }
            facets += "]";

            var request = new RestRequest("search")
                .AddParameter("index", sortOptions.ToString().ToLowerInvariant())
                .AddParameter("facets", facets)
                .AddParameter("limit", limit);

            if (!string.IsNullOrEmpty(query))
            {
                request.AddParameter("query", query);
            }

            var response = await _client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                var result = JsonSerializer.Deserialize<SearchResult>(response.Content);
                _logger.LogInformation($"Search completed successfully. Found {result.TotalHits} {_projectType}s.");
                return result;
            }
            else
            {
                _logger.LogError($"API request failed: {response.ErrorMessage}");
                throw new Exception($"API request failed: {response.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while searching for {_projectType}s");
            return null;
        }
    }

    /// <summary>
    /// Retrieves detailed information about a specific item from the Modrinth store.
    /// </summary>
    /// <param name="id">The unique identifier of the item.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the item details or null if an error occurred.</returns>
    public virtual async Task<StoreItem?> GetItemAsync(string id)
    {
        _logger.LogInformation($"Fetching {_projectType} with ID: {id}");

        try
        {
            var request = new RestRequest($"project/{id}");
            var response = await _client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                var item = JsonSerializer.Deserialize<StoreItem>(response.Content);
                _logger.LogInformation($"Successfully fetched {_projectType} with ID: {id}");
                return item;
            }
            else
            {
                _logger.LogError($"API request failed: {response.ErrorMessage}");
                throw new Exception($"API request failed: {response.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while fetching {_projectType} with ID: {id}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves all versions of a specific item from the Modrinth store.
    /// </summary>
    /// <param name="id">The unique identifier of the item.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of item versions or null if an error occurred.</returns>
    public virtual async Task<List<ItemVersion>?> GetVersionsAsync(string id)
    {
        _logger.LogInformation($"Fetching versions for {_projectType} with ID: {id}");

        try
        {
            var request = new RestRequest($"project/{id}/version");
            var response = await _client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                var versions = JsonSerializer.Deserialize<List<ItemVersion>>(response.Content);
                _logger.LogInformation($"Successfully fetched versions for {_projectType} with ID: {id}");
                return versions;
            }
            else
            {
                _logger.LogError($"API request failed: {response.ErrorMessage}");
                throw new Exception($"API request failed: {response.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while fetching versions for {_projectType} with ID: {id}");
            return null;
        }
    }

    /// <summary>
    /// Downloads a specific file for an item from the Modrinth store.
    /// </summary>
    /// <param name="file">The file information object containing download details.</param>
    /// <param name="projectType">The type of project being downloaded (e.g., "mods", "resourcepacks").</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    public virtual async Task DownloadItemAsync(ItemFile file, string projectType)
    {
        _logger.LogInformation($"Downloading {projectType} file from URL: {file.Url}");

        try
        {
            var request = new RestRequest(file.Url);
            var response = await _client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                var filePath = Path.Combine(MCPath.BasePath, projectType, file.Filename);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                await File.WriteAllBytesAsync(filePath, response.RawBytes);

                _logger.LogInformation($"Successfully downloaded {projectType} file to: {filePath}");
            }
            else
            {
                _logger.LogError($"File download failed: {response.ErrorMessage}");
                throw new Exception($"File download failed: {response.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while downloading {projectType} file from URL: {file.Url}");
            throw;
        }
    }
}
