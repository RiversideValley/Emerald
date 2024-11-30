using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Helpers;

namespace Emerald.CoreX.Store.Modrinth.JSON;

public class SearchResult
{
    [JsonPropertyName("hits")] public List<SearchHit> Hits { get; set; }

    [JsonPropertyName("offset")] public int Offset { get; set; }

    [JsonPropertyName("limit")] public int Limit { get; set; }

    [JsonPropertyName("total_hits")] public int TotalHits { get; set; }
}

public class Category
{
    public string icon { get; set; }
    public string name { get; set; }
    public string project_type { get; set; }
    public string header { get; set; }
}

public class SearchHit
{
    [JsonPropertyName("slug")] public string Slug { get; set; }

    [JsonPropertyName("title")] public string Title { get; set; }

    [JsonPropertyName("description")] public string Description { get; set; }

    [JsonPropertyName("categories")] public string[] Categories { get; set; }

    [JsonPropertyName("client_side")] public string ClientSide { get; set; }

    [JsonPropertyName("server_side")] public string ServerSide { get; set; }

    [JsonPropertyName("project_type")] public string ProjectType { get; set; }

    [JsonPropertyName("downloads")] public int Downloads { get; set; }

    [JsonPropertyName("icon_url")] public string IconUrl { get; set; }

    [JsonPropertyName("project_id")] public string ProjectId { get; set; }

    [JsonPropertyName("author")] public string Author { get; set; }

    [JsonPropertyName("versions")] public string[] Versions { get; set; }

    [JsonPropertyName("follows")] public int Follows { get; set; }

    [JsonPropertyName("date_created")] public DateTime DateCreated { get; set; }

    [JsonPropertyName("date_modified")] public DateTime DateModified { get; set; }

    [JsonPropertyName("latest_version")] public string LatestVersion { get; set; }

    [JsonPropertyName("license")] public string License { get; set; }

    [JsonPropertyName("gallery")] public string[] Gallery { get; set; }
}

public class StoreItem
{
    [JsonPropertyName("id")] public string ID { get; set; }

    [JsonPropertyName("slug")] public string Slug { get; set; }

    [JsonPropertyName("project_type")] public string ProjectType { get; set; }

    [JsonPropertyName("team")] public string Team { get; set; }

    [JsonPropertyName("title")] public string Title { get; set; }

    [JsonPropertyName("description")] public string Description { get; set; }

    [JsonPropertyName("body")] public string Body { get; set; }

    [JsonPropertyName("body_url")] public string BodyUrl { get; set; }

    [JsonPropertyName("published")] public DateTime PublishedDate { get; set; }

    [JsonPropertyName("updated")] public DateTime UpdatedDate { get; set; }

    [JsonPropertyName("status")] public string Status { get; set; }

    [JsonPropertyName("moderator_message")] public object? ModeratorMessage { get; set; }

    [JsonPropertyName("license")] public License License { get; set; }

    [JsonPropertyName("client_side")] public string ClientSide { get; set; }

    [JsonPropertyName("server_side")] public string ServerSide { get; set; }

    [JsonPropertyName("downloads")] public int Downloads { get; set; }

    [JsonPropertyName("followers")] public int Followers { get; set; }

    [JsonPropertyName("categories")] public string[] Categories { get; set; }

    [JsonPropertyName("versions")] public string[] Versions { get; set; }

    [JsonPropertyName("icon_url")] public string IconUrl { get; set; }

    [JsonPropertyName("issues_url")] public string IssuesUrl { get; set; }

    [JsonPropertyName("source_url")] public string SourceUrl { get; set; }

    [JsonPropertyName("wiki_url")] public object? WikiUrl { get; set; }

    [JsonPropertyName("discord_url")] public string DiscordUrl { get; set; }

    [JsonPropertyName("donation_urls")] public DonationUrls[] DonationUrls { get; set; }

    [JsonPropertyName("gallery")] public object[] Gallery { get; set; }
}

public class ItemVersion : INotifyPropertyChanged
{
    public bool IsDetailsVisible { get; set; } = false;
    public string? FileName => Files.FirstOrDefault(x => x.Primary)?.Filename;

    [JsonPropertyName("id")] public string ID { get; set; }

    [JsonPropertyName("project_id")] public string ProjectId { get; set; }

    [JsonPropertyName("author_id")] public string AuthorId { get; set; }

    [JsonPropertyName("featured")] public bool Featured { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("version_number")] public string VersionNumber { get; set; }

    [JsonPropertyName("changelog")] public string Changelog { get; set; }

    [JsonPropertyName("changelog_url")] public string? ChangelogUrl { get; set; }

    [JsonPropertyName("date_published")] public DateTime DatePublished { get; set; }

    [JsonPropertyName("downloads")] public int Downloads { get; set; }

    [JsonPropertyName("version_type")] public string VersionType { get; set; }

    [JsonPropertyName("files")] public ItemFile[] Files { get; set; }

    [JsonPropertyName("dependencies")] public Dependency[] Dependencies { get; set; }

    [JsonPropertyName("game_versions")] public string[] GameVersions { get; set; }

    [JsonPropertyName("loaders")] public string[] Loaders { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void InvokePropertyChanged(string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class Dependency
{
    [JsonPropertyName("version_id")] public string VersionId { get; set; }

    [JsonPropertyName("project_id")] public string ProjectId { get; set; }

    [JsonPropertyName("file_name")] public string FileName { get; set; }

    [JsonPropertyName("dependency_type")] public string DependencyType { get; set; }
}

public class ItemFile
{
    [JsonPropertyName("hashes")] public Hashes Hashes { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }

    [JsonPropertyName("filename")] public string Filename { get; set; }

    [JsonPropertyName("primary")] public bool Primary { get; set; }

    [JsonPropertyName("size")] public int Size { get; set; }
}

public class License
{
    [JsonPropertyName("id")] public string ID { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }
}

public class DonationUrls
{
    [JsonPropertyName("id")] public string ID { get; set; }

    [JsonPropertyName("platform")] public string Platform { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }
}
