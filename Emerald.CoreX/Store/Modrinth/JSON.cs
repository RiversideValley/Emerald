using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.CoreX.Store.Modrinth.JSON;

public class SearchResult
{
    [JsonProperty("hits")] public List<SearchHit> Hits { get; set; }

    [JsonProperty("offset")] public int Offset { get; set; }

    [JsonProperty("limit")] public int Limit { get; set; }

    [JsonProperty("total_hits")] public int TotalHits { get; set; }
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
    [JsonProperty("slug")] public string Slug { get; set; }

    [JsonProperty("title")] public string Title { get; set; }

    [JsonProperty("description")] public string Description { get; set; }

    [JsonProperty("categories")] public string[] Categories { get; set; }

    [JsonProperty("client_side")] public string ClientSide { get; set; }

    [JsonProperty("server_side")] public string ServerSide { get; set; }

    [JsonProperty("project_type")] public string ProjectType { get; set; }

    [JsonProperty("downloads")] public int Downloads { get; set; }

    [JsonProperty("icon_url")] public string IconUrl { get; set; }

    [JsonProperty("project_id")] public string ProjectId { get; set; }

    [JsonProperty("author")] public string Author { get; set; }

    [JsonProperty("versions")] public string[] Versions { get; set; }

    [JsonProperty("follows")] public int Follows { get; set; }

    [JsonProperty("date_created")] public DateTime DateCreated { get; set; }

    [JsonProperty("date_modified")] public DateTime DateModified { get; set; }

    [JsonProperty("latest_version")] public string LatestVersion { get; set; }

    [JsonProperty("license")] public string License { get; set; }

    [JsonProperty("gallery")] public string[] Gallery { get; set; }
}

public class StoreItem
{
    [JsonProperty("id")] public string ID { get; set; }

    [JsonProperty("slug")] public string Slug { get; set; }

    [JsonProperty("project_type")] public string ProjectType { get; set; }

    [JsonProperty("team")] public string Team { get; set; }

    [JsonProperty("title")] public string Title { get; set; }

    [JsonProperty("description")] public string Description { get; set; }

    [JsonProperty("body")] public string Body { get; set; }

    [JsonProperty("body_url")] public string BodyUrl { get; set; }

    [JsonProperty("published")] public DateTime PublishedDate { get; set; }

    [JsonProperty("updated")] public DateTime UpdatedDate { get; set; }

    [JsonProperty("status")] public string Status { get; set; }

    [JsonProperty("moderator_message")] public object? ModeratorMessage { get; set; }

    [JsonProperty("license")] public License License { get; set; }

    [JsonProperty("client_side")] public string ClientSide { get; set; }

    [JsonProperty("server_side")] public string ServerSide { get; set; }

    [JsonProperty("downloads")] public int Downloads { get; set; }

    [JsonProperty("followers")] public int Followers { get; set; }

    [JsonProperty("categories")] public string[] Categories { get; set; }

    [JsonProperty("versions")] public string[] Versions { get; set; }

    [JsonProperty("icon_url")] public string IconUrl { get; set; }

    [JsonProperty("issues_url")] public string IssuesUrl { get; set; }

    [JsonProperty("source_url")] public string SourceUrl { get; set; }

    [JsonProperty("wiki_url")] public object? WikiUrl { get; set; }

    [JsonProperty("discord_url")] public string DiscordUrl { get; set; }

    [JsonProperty("donation_urls")] public DonationUrls[] DonationUrls { get; set; }

    [JsonProperty("gallery")] public object[] Gallery { get; set; }
}

public class ItemVersion : INotifyPropertyChanged
{
    public bool IsDetailsVisible { get; set; } = false;
    public string? FileName => Files.FirstOrDefault(x => x.Primary)?.Filename;

    [JsonProperty("id")] public string ID { get; set; }

    [JsonProperty("project_id")] public string ProjectId { get; set; }

    [JsonProperty("author_id")] public string AuthorId { get; set; }

    [JsonProperty("featured")] public bool Featured { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("version_number")] public string VersionNumber { get; set; }

    [JsonProperty("changelog")] public string Changelog { get; set; }

    [JsonProperty("changelog_url")] public string? ChangelogUrl { get; set; }

    [JsonProperty("date_published")] public DateTime DatePublished { get; set; }

    [JsonProperty("downloads")] public int Downloads { get; set; }

    [JsonProperty("version_type")] public string VersionType { get; set; }

    [JsonProperty("files")] public ItemFile[] Files { get; set; }

    [JsonProperty("dependencies")] public Dependency[] Dependencies { get; set; }

    [JsonProperty("game_versions")] public string[] GameVersions { get; set; }

    [JsonProperty("loaders")] public string[] Loaders { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void InvokePropertyChanged(string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class Dependency
{
    [JsonProperty("version_id")] public string VersionId { get; set; }

    [JsonProperty("project_id")] public string ProjectId { get; set; }

    [JsonProperty("file_name")] public string FileName { get; set; }

    [JsonProperty("dependency_type")] public string DependencyType { get; set; }
}

public class ItemFile
{
    [JsonProperty("hashes")] public Hashes Hashes { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("filename")] public string Filename { get; set; }

    [JsonProperty("primary")] public bool Primary { get; set; }

    [JsonProperty("size")] public int Size { get; set; }
}

public class Hashes
{
    [JsonProperty("sha512")] public string Sha512 { get; set; }

    [JsonProperty("sha1")] public string Sha1 { get; set; }
}

public class License
{
    [JsonProperty("id")] public string ID { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("url")] public string Url { get; set; }
}

public class DonationUrls
{
    [JsonProperty("id")] public string ID { get; set; }

    [JsonProperty("platform")] public string Platform { get; set; }

    [JsonProperty("url")] public string Url { get; set; }
}
