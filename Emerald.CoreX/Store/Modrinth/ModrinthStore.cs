using CmlLib.Core;
using Newtonsoft.Json;
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
    protected ModrinthStore(MinecraftPath path, ILogger logger, string projectType)
    {
        _client = new RestClient("https://api.modrinth.com/v2/");
        _client.AddDefaultHeader("Accept", "application/json");
        MCPath = path;
        _logger = logger;
        _projectType = projectType;
    }

    public async Task LoadCategoriesAsync()
    {
        var request = new RestRequest("tag/category");

        try
        {
            var response = await _client.ExecuteAsync(request);
            if (response.IsSuccessful)
            {
                var all = JsonConvert.DeserializeObject<List<Category>>(response.Content);

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
                var result = JsonConvert.DeserializeObject<SearchResult>(response.Content);
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

    public virtual async Task<StoreItem?> GetItemAsync(string id)
    {
        _logger.LogInformation($"Fetching {_projectType} with ID: {id}");

        try
        {
            var request = new RestRequest($"project/{id}");
            var response = await _client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                var item = JsonConvert.DeserializeObject<StoreItem>(response.Content);
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

    public virtual async Task<List<ItemVersion>?> GetVersionsAsync(string id)
    {
        _logger.LogInformation($"Fetching versions for {_projectType} with ID: {id}");

        try
        {
            var request = new RestRequest($"project/{id}/version");
            var response = await _client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                var versions = JsonConvert.DeserializeObject<List<ItemVersion>>(response.Content);
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
