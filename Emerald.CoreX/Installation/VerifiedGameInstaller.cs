using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using CmlLib.Core;
using CmlLib.Core.Files;
using CmlLib.Core.Installers;

namespace Emerald.CoreX.Installation;

/// <summary>
/// CmlLib installer adapter that validates every source before replacing a live
/// file. CmlLib update tasks run only after the source file is known to be healthy.
/// </summary>
public sealed class VerifiedGameInstaller(HttpClient httpClient, INetworkCapabilityService network) : IGameInstaller
{
    public async ValueTask Install(
        IEnumerable<GameFile> gameFiles,
        IProgress<InstallerProgressChangedEventArgs>? fileProgress,
        IProgress<ByteProgress>? byteProgress,
        CancellationToken cancellationToken)
    {
        // Parent/child version extraction can emit the same shared file more than
        // once. Collapse those entries before doing any disk or network work.
        var files = gameFiles
            .GroupBy(x => Path.GetFullPath(x.Path), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();
        var completed = 0;
        long processedBytes = 0;
        var errors = new ConcurrentQueue<Exception>();

        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken }, async (file, token) =>
        {
            try
            {
                fileProgress?.Report(new(files.Length, Volatile.Read(ref completed), file.Name, InstallerEventType.Queued));
                if (!await IsHealthyAsync(file, token)) await DownloadVerifiedAsync(file, token);
                // Examples include native extraction and derived legacy mappings.
                await file.ExecuteUpdateTask(token);
                var done = Interlocked.Increment(ref completed);
                var bytes = Interlocked.Add(ref processedBytes, Math.Max(0, file.Size));
                fileProgress?.Report(new(files.Length, done, file.Name, InstallerEventType.Done));
                byteProgress?.Report(new(files.Sum(x => Math.Max(0, x.Size)), bytes));
            }
            catch (Exception ex) { errors.Enqueue(ex); }
        });

        if (errors.TryDequeue(out var error)) throw new AggregateException("One or more game files could not be installed safely.", errors.Prepend(error));
    }

    private static async Task<bool> IsHealthyAsync(GameFile file, CancellationToken cancellationToken)
    {
        if (!File.Exists(file.Path)) return false;
        var info = new FileInfo(file.Path);
        if (file.Size > 0 && info.Length != file.Size) return false;
        if (string.IsNullOrWhiteSpace(file.Hash)) return true;
        await using var stream = File.OpenRead(file.Path);
        var actual = Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken));
        return string.Equals(actual, file.Hash, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadVerifiedAsync(GameFile file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(file.Url)) throw new InvalidOperationException($"No repair URL is available for {file.Path}.");
        Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
        // Keeping the temporary file beside its destination makes the final move
        // an atomic same-volume replacement on supported filesystems.
        var temporary = file.Path + ".emerald-download-" + Guid.NewGuid().ToString("N");
        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var response = await httpClient.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    if (response.StatusCode == HttpStatusCode.NotFound) throw new NonRetryableDownloadException($"File was not found: {file.Url}");
                    response.EnsureSuccessStatusCode();
                    await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
                    await using (var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                        await source.CopyToAsync(destination, cancellationToken);

                    var downloaded = new GameFile(file.Name) { Path = temporary, Hash = file.Hash, Size = file.Size };
                    // Validation failures are deterministic and must not be retried
                    // or allowed to overwrite a previously healthy destination.
                    if (!await IsHealthyAsync(downloaded, cancellationToken))
                        throw new NonRetryableDownloadException($"Downloaded file failed validation: {file.Name}");
                    File.Move(temporary, file.Path, true);
                    network.ReportSuccess(NetworkCapability.MinecraftFiles);
                    return;
                }
                catch (Exception ex) when (attempt < 3 && ex is not NonRetryableDownloadException && ex is not OperationCanceledException)
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            network.ReportFailure(NetworkCapability.MinecraftFiles, ex);
            throw;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class NonRetryableDownloadException(string message) : Exception(message);
}
