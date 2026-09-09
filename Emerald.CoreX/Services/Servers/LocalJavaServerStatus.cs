using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Emerald.CoreX.Services.Servers;

public interface IServerAddressClassifier { Task<bool> IsLocalAsync(string host, CancellationToken token); }

/// <summary>Read-only Java status transport. Never sends local endpoints to a web service.</summary>
public static class LocalJavaServerStatus
{
    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip)
            || ip.IsIPv6LinkLocal
            || ip.IsIPv6SiteLocal)
            return true;

        var bytes = ip.GetAddressBytes();
        return bytes.Length == 16
            ? (bytes[0] & 0xFE) == 0xFC
            : bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0
              || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
              || (bytes[0] == 192 && bytes[1] == 168)
              || (bytes[0] == 169 && bytes[1] == 254);
    }

    public static async Task<bool> IsLocalAsync(string host, CancellationToken token)
    {
        if (IPAddress.TryParse(host, out var ip))
            return IsPrivate(ip);

        if (!host.Contains('.')
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        // Fail closed on DNS errors: an unresolved private name must never escape to a provider.
        var addresses = await Dns.GetHostAddressesAsync(host, token);
        return addresses.Length == 0 || addresses.Any(IsPrivate);
    }

    public static async Task<ServerStatusSnapshot> QueryAsync(MinecraftServerAddress address, CancellationToken token)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(address.Host, address.Port, token);

        using var stream = client.GetStream();
        using var handshake = new MemoryStream();
        WriteVarInt(handshake, 0);
        WriteVarInt(handshake, -1); // Status negotiation, independent of the selected game version.

        var host = Encoding.UTF8.GetBytes(address.Host);
        WriteVarInt(handshake, host.Length);
        handshake.Write(host);

        handshake.WriteByte((byte)(address.Port >> 8));
        handshake.WriteByte((byte)address.Port);

        WriteVarInt(handshake, 1);

        await WritePacketAsync(stream, handshake.ToArray(), token);
        await WritePacketAsync(stream, [0], token);

        var packet = await ReadPacketAsync(stream, token);

        using var response = new MemoryStream(packet);

        if (ReadVarInt(response) != 0)
            throw new InvalidDataException("Unexpected status packet.");
        var length = ReadVarInt(response);

        if (length < 0 || length > response.Length - response.Position)
            throw new InvalidDataException("Invalid status length.");

        var json = new byte[length];
        response.ReadExactly(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var players = root.TryGetProperty("players", out var p) ? p : default;
        var version = root.TryGetProperty("version", out var v) ? v : default;
        var motd = root.TryGetProperty("description", out var d) ? CleanText(d) : null;
        var icon = Text(root, "favicon");

        if (icon != null && !ValidIcon(icon))
            icon = null;

        long? latency = null;

        try
        {
            byte[] ping = [1, 0, 0, 0, 0, 0, 0, 0, 1];
            var watch = Stopwatch.StartNew();
            await WritePacketAsync(stream, ping, token);
            var pong = await ReadPacketAsync(stream, token);

            if (pong.AsSpan().SequenceEqual(ping))
                latency = watch.ElapsedMilliseconds;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException)
        {

        }

        return new(
            ServerStatusState.Online,
            DateTimeOffset.UtcNow,
            address,
            Version: Text(version, "name"),
            Protocol: Number(version, "protocol"),
            Motd: motd,
            Players: Number(players, "online") ?? 0,
            MaxPlayers: Number(players, "max") ?? 0,
            IconDataUrl: icon,
            Source: ServerStatusSource.Local,
            LatencyMilliseconds: latency);
    }

    public static bool ValidIcon(string icon)
    {
        const string prefix = "data:image/png;base64,";
        if (!icon.StartsWith(prefix, StringComparison.Ordinal)
            || icon.Length > 128 * 1024)
            return false;

        try
        {
            var bytes = Convert.FromBase64String(icon[prefix.Length..]);

            return bytes.Length >= 24
                   && bytes.AsSpan(0, 8)
                       .SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                   && System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) == 64
                   && System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) == 64;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? Text(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int? Number(JsonElement e, string key) => 
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(key, out var v) &&
        v.ValueKind == JsonValueKind.Number &&
        v.TryGetInt32(out var n)
        ? n
        : null;

    private static string CleanText(JsonElement e, int depth = 0)
    {
        if (depth > 16) return string.Empty;
        var text = e.ValueKind == JsonValueKind.String
            ? e.GetString() ?? string.Empty
            : Text(e, "text") ?? string.Empty;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("extra", out var extra) &&
            extra.ValueKind == JsonValueKind.Array)
            foreach (var child in extra.EnumerateArray().Take(100))
                text += CleanText(child, depth + 1);
        return System.Text.RegularExpressions.Regex.Replace(text, "§.", string.Empty);
    }

    private static async Task WritePacketAsync(Stream stream, byte[] payload, CancellationToken token)
    {
        using var packet = new MemoryStream();
        WriteVarInt(packet, payload.Length);
        packet.Write(payload);
        await stream.WriteAsync(packet.ToArray(), token);
    }

    private static async Task<byte[]> ReadPacketAsync(Stream stream, CancellationToken token)
    {
        var length = 0;
        var b = new byte[1];
        for (var i = 0;; i++)
        {
            if (i == 5) throw new InvalidDataException("Oversized VarInt.");
            await stream.ReadExactlyAsync(b, token);
            length |= (b[0] & 127) << (7 * i);
            if ((b[0] & 128) == 0) break;
        }

        if (length <= 0 || length > 256 * 1024) throw new InvalidDataException("Oversized status packet.");
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, token);
        return data;
    }

    private static int ReadVarInt(Stream stream)
    {
        var value = 0;
        for (var i = 0; i < 5; i++)
        {
            var b = stream.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            value |= (b & 127) << (7 * i);
            if ((b & 128) == 0) return value;
        }

        throw new InvalidDataException("Oversized VarInt.");
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var remaining = (uint)value;
        do
        {
            var b = (byte)(remaining & 127);
            remaining >>= 7;
            stream.WriteByte(remaining == 0 ? b : (byte)(b | 128));
        } while (remaining != 0);
    }
}
