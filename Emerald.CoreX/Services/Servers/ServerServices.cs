using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Installation;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Servers;

public readonly record struct MinecraftServerAddress(string Host, int Port)
{
    public string DisplayAddress => Host.Contains(':') ? $"[{Host}]:{Port}" : Port == 25565 ? Host : $"{Host}:{Port}";
    public string CanonicalKey => $"{Host.Trim().TrimEnd('.').ToLowerInvariant()}:{Port}";
}

public static class MinecraftServerAddressParser
{
    public static bool TryParse(string? input, out MinecraftServerAddress address, out string? error)
    {
        address = default;
        error = null;
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Enter a server address.";
            return false;
        }

        if (value.Contains("://", StringComparison.Ordinal) ||
            value.Any(char.IsWhiteSpace) ||
            value.IndexOfAny(['/', '?', '#']) >= 0)
        {
            error = "Enter a host name or IP address without a URI scheme, path, or spaces.";
            return false;
        }

        string host;
        var port = 25565;
        if (value.StartsWith('['))
        {
            var close = value.IndexOf(']');
            if (close < 2 || (close + 1 < value.Length && value[close + 1] != ':'))
            {
                error = "The bracketed IPv6 address is invalid.";
                return false;
            }

            host = value[1..close];

            if (close + 1 < value.Length && !TryPort(value[(close + 2)..], out port, out error))
            {
                return false;
            }

            if (!IPAddress.TryParse(host, out var parsed) ||
                parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                error = "The bracketed address is not valid IPv6.";
                return false;
            }
        }
        else
        {
            var colonCount = value.Count(c => c == ':');
            if (colonCount > 1)
            {
                if (!IPAddress.TryParse(value, out var parsed) ||
                    parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    error = "The IPv6 address is invalid.";
                    return false;
                }

                host = value;
            }
            else if (colonCount == 1)
            {
                var index = value.LastIndexOf(':');
                host = value[..index];
                if (!TryPort(value[(index + 1)..], out port, out error))
                {
                    return false;
                }
            }
            else
            {
                host = value;
            }
        }

        if (string.IsNullOrWhiteSpace(host) || host is "." or "-")
        {
            error = "The host name is invalid.";
            return false;
        }

        address = new MinecraftServerAddress(host, port);
        return true;
    }

    public static MinecraftServerAddress Parse(string input)
    {
        return TryParse(input, out var address, out var error) ? address : throw new FormatException(error);
    }

    private static bool TryPort(string value, out int port, out string? error)
    {
        error = null;
        if (!int.TryParse(value, out port) || port is < 1 or > 65535)
        {
            error = "The port must be between 1 and 65535.";
            return false;
        }

        return true;
    }
}

public enum SavedServerSourceKind
{
    Directory,
    Custom
}

public sealed class SavedServer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25565;
    public SavedServerSourceKind SourceKind { get; set; }
    public string? SourceSlug { get; set; }
    public string? SourcePageUrl { get; set; }
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }
    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLaunchedAt { get; set; }
    [JsonIgnore] public string Address => new MinecraftServerAddress(Host, Port).DisplayAddress;
}

public interface ISavedServerService
{
    event EventHandler? Changed;
    IReadOnlyList<SavedServer> GetAll();
    SavedServer Save(SavedServer server);
    bool Remove(Guid id);
    SavedServer? Find(Guid id);
    void MarkLaunched(Guid id, DateTimeOffset? at = null);
}

internal sealed class SavedServerEnvelope
{
    public int SchemaVersion { get; set; } = 1;
    public List<SavedServer> Servers { get; set; } = [];
}

public sealed class SavedServerService(IBaseSettingsService settings, ILogger<SavedServerService> logger)
    : ISavedServerService
{
    private readonly object _gate = new();
    private bool _isReadOnly;
    public event EventHandler? Changed;

    public IReadOnlyList<SavedServer> GetAll()
    {
        lock (_gate)
        {
            return Read().Servers.OrderByDescending(x => x.LastLaunchedAt ?? x.DateAdded).ToArray();
        }
    }

    public SavedServer? Find(Guid id)
    {
        return GetAll().FirstOrDefault(x => x.Id == id);
    }

    public SavedServer Save(SavedServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var parsed =
            MinecraftServerAddressParser.Parse(new MinecraftServerAddress(server.Host, server.Port).DisplayAddress);

        lock (_gate)
        {
            var envelope = Read();

            if (_isReadOnly)
            {
                return server;
            }

            var duplicate = envelope.Servers
                .FirstOrDefault(x =>
                    new MinecraftServerAddress(x.Host, x.Port).CanonicalKey == parsed.CanonicalKey
                    && x.Id != server.Id);

            if (duplicate != null)
            {
                duplicate.Name = string.IsNullOrWhiteSpace(server.Name) ? duplicate.Name : server.Name.Trim();
                Write(envelope);
                Changed?.Invoke(this, EventArgs.Empty);
                return duplicate;
            }

            var existing =
                envelope.Servers.FirstOrDefault(x => x.Id == server.Id);

            if (existing == null)
            {
                envelope.Servers.Add(server);
            }
            else
            {
                Copy(server, existing);
            }

            Write(envelope);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return server;
    }

    public bool Remove(Guid id)
    {
        bool removed;
        lock (_gate)
        {
            var envelope = Read();
            if (_isReadOnly)
            {
                return false;
            }

            removed = envelope.Servers.RemoveAll(x => x.Id == id) > 0;
            if (removed)
            {
                Write(envelope);
            }
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    public void MarkLaunched(Guid id, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            var envelope = Read();
            if (_isReadOnly)
            {
                return;
            }

            var server = envelope.Servers.FirstOrDefault(x => x.Id == id);
            if (server == null)
            {
                return;
            }

            server.LastLaunchedAt = at ?? DateTimeOffset.UtcNow;
            Write(envelope);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private SavedServerEnvelope Read()
    {
        var value = settings.Get(SettingsKeys.SavedServers, new SavedServerEnvelope());
        if (value.SchemaVersion > 1)
        {
            _isReadOnly = true;
            logger.LogWarning(
                "Saved server schema {Schema} is newer than this Emerald build; saved servers are read-only.",
                value.SchemaVersion);
        }

        return value;
    }

    private void Write(SavedServerEnvelope value)
    {
        settings.Set(SettingsKeys.SavedServers, value);
    }

    private static void Copy(SavedServer source, SavedServer destination)
    {
        destination.Name = source.Name;
        destination.Host = source.Host;
        destination.Port = source.Port;
        destination.SourceKind = source.SourceKind;
        destination.SourceSlug = source.SourceSlug;
        destination.SourcePageUrl = source.SourcePageUrl;
        destination.IconUrl = source.IconUrl;
        destination.BannerUrl = source.BannerUrl;
        destination.LastLaunchedAt = source.LastLaunchedAt;
    }
}

public enum ServerDirectorySort
{
    Votes,
    Players,
    Rating,
    Newest,
    Name
}

public sealed record ServerDirectoryQuery(
    string? Search = null,
    string? Tag = null,
    string? Version = null,
    ServerDirectorySort Sort = ServerDirectorySort.Votes,
    int Page = 1,
    int PageSize = 20);

public sealed record ServerDirectoryEntry(
    string Name,
    string Host,
    int Port,
    string? Slug,
    string? PageUrl,
    string? Motd,
    string? Version,
    int Players,
    int MaxPlayers,
    string? IconUrl,
    string? BannerUrl,
    IReadOnlyList<string> Tags,
    double? Rating,
    int Votes,
    bool Online)
{
    public string Address => new MinecraftServerAddress(Host, Port).DisplayAddress;
    public string PlayerText => Online ? $"{Players:N0} / {MaxPlayers:N0} players" : "Offline";
    public string TagsText => string.Join(" • ", Tags.Take(4));
}

public sealed record ServerDirectoryPage(
    IReadOnlyList<ServerDirectoryEntry> Servers,
    int Page,
    int PageSize,
    int? Total);

public interface IServerDirectoryService
{
    Task<ServerDirectoryPage> SearchAsync(ServerDirectoryQuery query, CancellationToken cancellationToken = default);
}

public sealed class ServerDirectoryService(
    HttpClient httpClient,
    INetworkCapabilityService network,
    ILogger<ServerDirectoryService> logger) : IServerDirectoryService
{
    private sealed record CacheEntry(DateTimeOffset At, ServerDirectoryPage Page);

    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly object _gate = new();

    public async Task<ServerDirectoryPage> SearchAsync(ServerDirectoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            ["edition"] = "java",
            ["page"] = Math.Max(1, query.Page).ToString(),
            ["per"] = Math.Clamp(query.PageSize, 1, 50).ToString(),
            ["sort"] = SortName(query.Sort)
        };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parameters["q"] = query.Search.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            parameters["tag"] = query.Tag.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.Version))
        {
            parameters["version"] = query.Version.Trim();
        }

        var queryString = string.Join('&',
            parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        var url = "https://minecraftserve.rs/api/servers?" + queryString;

        lock (_gate)
        {
            if (_cache.TryGetValue(url, out var hit) && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromMinutes(1))
            {
                return hit.Page;
            }
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await httpClient.GetAsync(url, deadline.Token);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(deadline.Token));
            var page = ParseDirectory(document.RootElement, query);

            lock (_gate)
            {
                _cache[url] = new CacheEntry(DateTimeOffset.UtcNow, page);
            }

            network.ReportSuccess(NetworkCapability.ServerDirectory);
            return page;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            network.ReportFailure(NetworkCapability.ServerDirectory, ex);
            logger.LogWarning(ex, "Server directory request failed.");
            throw;
        }
    }

    private static ServerDirectoryPage ParseDirectory(JsonElement root, ServerDirectoryQuery query)
    {
        var array = root.ValueKind == JsonValueKind.Array ? root : Find(root, "servers", "data", "results");
        var items = new List<ServerDirectoryEntry>();

        if (array.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in array.EnumerateArray())
            {
                var addressText = Text(x, "address", "ip", "host") ?? string.Empty;
                MinecraftServerAddressParser.TryParse(addressText, out var parsed, out _);
                var host = Text(x, "host", "ip") ?? parsed.Host ?? string.Empty;
                var port = Int(x, "port") ?? (parsed.Port == 0 ? 25565 : parsed.Port);
                var players = Find(x, "players");
                var votes = Find(x, "votes");
                var rating = Find(x, "rating");

                items.Add(new ServerDirectoryEntry(Text(x, "name", "title") ?? host,
                    host,
                    port,
                    Text(x, "slug"),
                    Text(x, "url", "website"),
                    Text(x, "motd", "tagline", "description"),
                    Text(x, "version"),
                    Int(players, "online") ?? Int(x, "online_players") ?? 0,
                    Int(players, "max") ?? Int(x, "max_players", "maxPlayers") ?? 0,
                    Text(x, "icon", "icon_url"),
                    Text(x, "banner", "banner_url"),
                    Strings(x, "tags"),
                    Double(rating, "average") ??
                    Double(x, "rating"),
                    Int(votes, "month") ??
                    Int(x, "votes") ?? 0,
                    Bool(x, "online") ?? true));
            }
        }

        return new ServerDirectoryPage(items, query.Page, query.PageSize, Int(root, "total", "count"));
    }

    private static JsonElement Find(JsonElement x, params string[] names)
    {
        foreach (var n in names)
        {
            if (x.ValueKind == JsonValueKind.Object && x.TryGetProperty(n, out var v))
            {
                return v;
            }
        }

        return default;
    }

    private static string? Text(JsonElement x, params string[] names)
    {
        var v = Find(x, names);
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static int? Int(JsonElement x, params string[] names)
    {
        var v = Find(x, names);
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;
    }

    private static double? Double(JsonElement x, params string[] names)
    {
        var v = Find(x, names);
        return v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) ? n : null;
    }

    private static bool? Bool(JsonElement x, params string[] names)
    {
        var v = Find(x, names);
        return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
    }

    private static IReadOnlyList<string> Strings(JsonElement x, params string[] names)
    {
        var v = Find(x, names);
        return v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(y => y.ValueKind == JsonValueKind.String).Select(y => y.GetString()!).ToArray()
            : [];
    }

    private static string SortName(ServerDirectorySort sort)
    {
        return sort switch
        {
            ServerDirectorySort.Players => "players",
            ServerDirectorySort.Rating => "rating",
            ServerDirectorySort.Newest => "new",
            ServerDirectorySort.Name => "name",
            _ => "votes"
        };
    }
}

public enum ServerStatusState
{
    Loading,
    Online,
    Offline,
    Stale,
    Unavailable,
    NoResponse
}

public enum ServerStatusSource
{
    Public,
    Local
}

public sealed record ServerStatusSnapshot(
    ServerStatusState State,
    DateTimeOffset CheckedAt,
    MinecraftServerAddress RequestedAddress,
    string? ResolvedIp = null,
    int? ResolvedPort = null,
    string? Version = null,
    int? Protocol = null,
    string? Software = null,
    string? Motd = null,
    int Players = 0,
    int MaxPlayers = 0,
    string? Map = null,
    bool? EulaBlocked = null,
    string? IconDataUrl = null,
    IReadOnlyList<string>? Plugins = null,
    IReadOnlyList<string>? Mods = null,
    string? Error = null,
    ServerStatusSource Source = ServerStatusSource.Public,
    long? LatencyMilliseconds = null);

public interface IServerStatusService
{
    Task<ServerStatusSnapshot> GetStatusAsync(MinecraftServerAddress address, bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public sealed class ServerStatusService(
    HttpClient httpClient,
    INetworkCapabilityService network,
    ILogger<ServerStatusService> logger,
    IServerAddressClassifier? classifier = null) : IServerStatusService
{
    private sealed record CacheEntry(DateTimeOffset At, ServerStatusSnapshot Snapshot);

    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly SemaphoreSlim _limit = new(4);
    private readonly object _gate = new();

    public async Task<ServerStatusSnapshot> GetStatusAsync(MinecraftServerAddress address, bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!forceRefresh
                && _cache.TryGetValue(address.CanonicalKey, out var hit)
                && DateTimeOffset.UtcNow - hit.At < (hit.Snapshot.Source == ServerStatusSource.Local
                    ? TimeSpan.FromSeconds(30)
                    : TimeSpan.FromMinutes(5)))
            {
                return hit.Snapshot;
            }
        }

        await _limit.WaitAsync(cancellationToken);
        var local = false;

        try
        {
            using var localDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localDeadline.CancelAfter(TimeSpan.FromSeconds(3));
            local = await (classifier?.IsLocalAsync(address.Host, localDeadline.Token)
                           ?? LocalJavaServerStatus.IsLocalAsync(address.Host, localDeadline.Token));

            if (local)
            {
                var result = await LocalJavaServerStatus.QueryAsync(address, localDeadline.Token);
                cancellationToken.ThrowIfCancellationRequested();

                lock (_gate)
                {
                    _cache[address.CanonicalKey] = new CacheEntry(DateTimeOffset.UtcNow, result);
                }

                return result;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.mcsrvstat.us/3/" + Uri.EscapeDataString(address.DisplayAddress));
            request.Headers.UserAgent.ParseAdd("Emerald-Launcher/1.0 (Minecraft launcher; status lookup)");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(12));
            using var response = await httpClient.SendAsync(request, deadline.Token);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(deadline.Token));

            var snapshot = ParseStatus(document.RootElement, address);

            lock (_gate)
            {
                _cache[address.CanonicalKey] = new CacheEntry(DateTimeOffset.UtcNow, snapshot);
            }

            network.ReportSuccess(NetworkCapability.ServerStatus);
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException
                                       or System.Net.Sockets.SocketException or IOException)
        {
            if (!local && ex is HttpRequestException)
            {
                network.ReportFailure(NetworkCapability.ServerStatus, ex);
            }

            lock (_gate)
            {
                if (_cache.TryGetValue(address.CanonicalKey, out var old))
                {
                    return old.Snapshot with
                    {
                        State = ServerStatusState.Stale, Error = ex.Message
                    };
                }
            }

            logger.LogWarning(ex, "Status lookup failed for a private server address.");

            return new ServerStatusSnapshot(
                local ? ServerStatusState.NoResponse : ServerStatusState.Unavailable,
                DateTimeOffset.UtcNow,
                address,
                Error: ex.Message,
                Source: local ? ServerStatusSource.Local : ServerStatusSource.Public);
        }
        finally
        {
            _limit.Release();
        }
    }

    private static ServerStatusSnapshot ParseStatus(JsonElement x, MinecraftServerAddress requested)
    {
        var online = x.TryGetProperty("online", out var o) && o.ValueKind == JsonValueKind.True;
        var players = x.TryGetProperty("players", out var p) ? p : default;

        var motd = x.TryGetProperty("motd", out var m) &&
                   m.TryGetProperty("clean", out var clean)
            ? string.Join(Environment.NewLine, clean.EnumerateArray().Select(y => y.GetString()))
            : null;

        return new ServerStatusSnapshot(online ? ServerStatusState.Online : ServerStatusState.Offline,
            DateTimeOffset.UtcNow, requested,
            Text(x, "ip"),
            Int(x, "port"),
            Text(x, "version"),
            x.TryGetProperty("protocol", out var protocol) ? Int(protocol, "version") : null,
            Text(x, "software"),
            motd,
            Int(players, "online") ?? 0,
            Int(players, "max") ?? 0,
            Text(x, "map"),
            Bool(x, "eula_blocked"),
            Text(x, "icon"),
            Names(x, "plugins"),
            Names(x, "mods"));
    }

    private static string? Text(JsonElement x, string name)
    {
        return x.ValueKind == JsonValueKind.Object && x.TryGetProperty(name, out var v) &&
               v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }

    private static int? Int(JsonElement x, string name)
    {
        return x.ValueKind == JsonValueKind.Object && x.TryGetProperty(name, out var v) && v.TryGetInt32(out var n)
            ? n
            : null;
    }

    private static bool? Bool(JsonElement x, string name)
    {
        return x.ValueKind == JsonValueKind.Object && x.TryGetProperty(name, out var v) &&
               v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : null;
    }

    private static IReadOnlyList<string> Names(JsonElement x, string name)
    {
        return x.ValueKind == JsonValueKind.Object
               && x.TryGetProperty(name, out var v)
               && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray()
                .Take(25)
                .Select(y => y.ValueKind == JsonValueKind.Object ? Text(y, "name") : y.GetString())
                .Where(y => y != null)
                .Cast<string>().ToArray()
            : [];
    }
}
