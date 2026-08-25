using System.Text.Json;

namespace Emerald.CoreX.Installation;

public interface IInstallationStateStore
{
    string GetReceiptPath(Game game);
    Task<InstanceInstallReceipt?> ReadAsync(Game game, CancellationToken cancellationToken = default);
    Task WriteAsync(Game game, InstanceInstallReceipt receipt, CancellationToken cancellationToken = default);
}

public sealed class InstallationStateStore : IInstallationStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string GetReceiptPath(Game game) => Path.Combine(game.Path.BasePath, ".emerald", "install-state.v1.json");

    public async Task<InstanceInstallReceipt?> ReadAsync(Game game, CancellationToken cancellationToken = default)
    {
        var path = GetReceiptPath(game);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<InstanceInstallReceipt>(stream, JsonOptions, cancellationToken);
    }

    public async Task WriteAsync(Game game, InstanceInstallReceipt receipt, CancellationToken cancellationToken = default)
    {
        var path = GetReceiptPath(game);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, receipt, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
