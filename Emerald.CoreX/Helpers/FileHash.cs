using System.Security.Cryptography;

namespace Emerald.CoreX.Helpers;

internal static class FileHash
{
    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    public static Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken)
        => ComputeHashAsync(SHA1.Create(), filePath, cancellationToken);

    public static string ComputeSha1(string filePath)
        => ComputeHash(SHA1.Create(), filePath);

    public static async Task<string> ComputeHashAsync(
        HashAlgorithm algorithm,
        string filePath,
        CancellationToken cancellationToken)
    {
        using (algorithm)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await algorithm.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public static string ComputeHash(HashAlgorithm algorithm, string filePath)
    {
        using (algorithm)
        {
            using var stream = File.OpenRead(filePath);
            var hash = algorithm.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public static async Task<bool> VerifyAsync(
        string filePath,
        string? sha1,
        string? sha512,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sha1))
        {
            var actualSha1 = await ComputeSha1Async(filePath, cancellationToken);
            if (!actualSha1.Equals(sha1, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(sha512))
        {
            var actualSha512 = await ComputeHashAsync(SHA512.Create(), filePath, cancellationToken);
            if (!actualSha512.Equals(sha512, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static bool Verify(string filePath, string? sha1, string? sha512)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sha1)
            && !ComputeSha1(filePath).Equals(sha1, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sha512)
            && !ComputeHash(SHA512.Create(), filePath).Equals(sha512, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
