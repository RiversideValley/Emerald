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
public sealed class VerifiedGameInstaller(
    HttpClient httpClient,
    INetworkCapabilityService network,
    DownloadTimeouts? timeouts = null) : IGameInstaller
{
    private readonly DownloadTimeouts _timeouts = timeouts ?? new DownloadTimeouts();

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
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = operation.Token }, async (file, token) =>
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
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (operation.IsCancellationRequested)
                {
                    // Another file has already reported the terminal failure.
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                    operation.Cancel();
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            // A sibling failed and cancelled the parallel work. The queued
            // original error below is the useful result for the caller.
        }

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
        if (string.IsNullOrWhiteSpace(file.Url)) throw new InvalidOperationException($"No download URL is available for {file.Path}.");
        var destinationPath = file.Path!;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        // Keeping the temporary file beside its destination makes the final move
        // an atomic same-volume replacement on supported filesystems.
        var temporary = destinationPath + ".emerald-download-" + Guid.NewGuid().ToString("N");
        try
        {
            await DownloadWithRetriesAsync(file, temporary, cancellationToken);
            File.Move(temporary, destinationPath, true);
            network.ReportSuccess(NetworkCapability.MinecraftFiles);
        }
        catch (Exception ex)
        {
            network.ReportFailure(NetworkCapability.MinecraftFiles, ex);
            throw;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private async Task DownloadWithRetriesAsync(GameFile file, string temporary, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _timeouts.Attempts; attempt++)
        {
            try
            {
                await DownloadAttemptAsync(file, temporary, cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DeleteTemporaryFile(temporary);
                if (attempt == _timeouts.Attempts) throw new DownloadTimeoutException("response", file.Url!);
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                DeleteTemporaryFile(temporary);
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
        }
    }

    private async Task DownloadAttemptAsync(GameFile file, string temporary, CancellationToken cancellationToken)
    {
        using var headersDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        headersDeadline.CancelAfter(_timeouts.ResponseHeadersTimeout);
        using var response = await httpClient.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, headersDeadline.Token);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new NonRetryableDownloadException($"File was not found: {file.Url}");
        if (!response.IsSuccessStatusCode && (int)response.StatusCode < 500)
            throw new NonRetryableDownloadException($"Download failed with HTTP {(int)response.StatusCode}: {file.Url}");
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            await CopyWithInactivityTimeoutAsync(source, destination, file.Url!, cancellationToken);

        var downloaded = new GameFile(file.Name) { Path = temporary, Hash = file.Hash, Size = file.Size };
        if (!await IsHealthyAsync(downloaded, cancellationToken))
            throw new NonRetryableDownloadException($"Downloaded file failed validation: {file.Name}");
    }

    private bool ShouldRetry(Exception exception, int attempt)
        => attempt < _timeouts.Attempts && exception is not NonRetryableDownloadException and not OperationCanceledException;

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);

    private static void DeleteTemporaryFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private async Task CopyWithInactivityTimeoutAsync(Stream source, Stream destination, string url, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        using var inactivity = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        inactivity.CancelAfter(_timeouts.InactivityTimeout);
        while (true)
        {
            int read;
            try { read = await source.ReadAsync(buffer.AsMemory(), inactivity.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new DownloadTimeoutException("transfer", url);
            }
            if (read == 0) return;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            inactivity.CancelAfter(_timeouts.InactivityTimeout);
        }
    }

    private sealed class NonRetryableDownloadException(string message) : Exception(message);
}
