namespace Emerald.CoreX.Store;

internal static class StorePath
{
    public static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static bool EqualsPath(string left, string right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    public static bool IsInsideRoot(string path, string root)
    {
        var normalizedPath = Normalize(path);
        var normalizedRoot = Normalize(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    public static long? GetFileSize(string filePath)
        => File.Exists(filePath) ? new FileInfo(filePath).Length : null;

    public static bool IsReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(System.IO.FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    public static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path) || IsReparsePoint(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
