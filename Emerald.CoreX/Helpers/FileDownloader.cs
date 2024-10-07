using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Helpers;

public class FileDownloader
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public FileDownloader(ILogger logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Downloads a file from the specified URL to the given file path.
    /// </summary>
    /// <param name="url">The URL of the file to download.</param>
    /// <param name="filePath">The path where the file should be saved.</param>
    /// <param name="expectedHashes">Optional. The expected hashes for file integrity verification.</param>
    /// <param name="progress">Optional. An IProgress{double} to report download progress.</param>
    /// <param name="cancellationToken">Optional. A CancellationToken to support cancellation of the download.</param>
    /// <returns>A task representing the asynchronous download operation.</returns>
    public async Task DownloadFileAsync(string url, string filePath, Hashes? expectedHashes = null,
    IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Downloading file from URL: {url}");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            // Ensure any existing file is closed before overwriting
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytes > 0 ? (double)totalBytesRead / totalBytes * 100 : 0);
            }

            fileStream.Dispose();
            contentStream.Dispose();
            _logger.LogInformation($"Successfully downloaded file to: {filePath}");

            if (expectedHashes != null && !await VerifyFileIntegrityAsync(filePath, expectedHashes))
            {
                throw new Exception("Downloaded file failed integrity check");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while downloading file from URL: {url}");
            throw;
        }
    }


    /// <summary>
    /// Verifies the integrity of a file against expected hashes.
    /// </summary>
    /// <param name="filePath">The path of the file to verify.</param>
    /// <param name="expectedHashes">The expected hashes for the file.</param>
    /// <returns>True if the file integrity is verified, false otherwise.</returns>
    private async Task<bool> VerifyFileIntegrityAsync(string filePath, Hashes expectedHashes)
    {
        try
        {
            using var sha1 = SHA1.Create();
            using var sha512 = SHA512.Create();

            // Open the file with read-only access and allow other processes to read the file
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Compute SHA-1 hash
            var sha1Hash = BitConverter.ToString(await sha1.ComputeHashAsync(stream)).Replace("-", "").ToLowerInvariant();

            // Reset stream position for the next hash computation
            stream.Position = 0;

            // Compute SHA-512 hash
            var sha512Hash = BitConverter.ToString(await sha512.ComputeHashAsync(stream)).Replace("-", "").ToLowerInvariant();

            return sha1Hash == expectedHashes.Sha1 && sha512Hash == expectedHashes.Sha512;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred during file integrity verification for file: {filePath}");
            throw;
        }
    }

}


public class Hashes
{
    [JsonPropertyName("sha512")] public string Sha512 { get; set; }

    [JsonPropertyName("sha1")] public string Sha1 { get; set; }
}
