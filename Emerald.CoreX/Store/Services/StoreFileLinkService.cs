using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Store;

public interface IStoreFileLinkService
{
    StoreLinkCreationResult CreateLinkOrCopy(string sourcePath, string targetPath, StoreLinkMode preferredMode);

    StoreLinkCreationResult ReplaceWithLinkOrCopy(string sourcePath, string targetPath, StoreLinkMode preferredMode);

    bool AreOnSameRoot(string sourcePath, string targetPath);

    bool IsSymbolicLink(string path);

    string? GetSymbolicLinkTarget(string path);
}

public sealed class StoreFileLinkService(ILogger<StoreFileLinkService> logger) : IStoreFileLinkService
{
    public StoreLinkCreationResult CreateLinkOrCopy(string sourcePath, string targetPath, StoreLinkMode preferredMode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        if (File.Exists(targetPath) || Directory.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        return TryCreateLinkOrCopy(sourcePath, targetPath, preferredMode);
    }

    public StoreLinkCreationResult ReplaceWithLinkOrCopy(string sourcePath, string targetPath, StoreLinkMode preferredMode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        if (!File.Exists(targetPath) && !IsSymbolicLink(targetPath))
        {
            return CreateLinkOrCopy(sourcePath, targetPath, preferredMode);
        }

        var tempTarget = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".emerald-link-{Guid.NewGuid():N}{Path.GetExtension(targetPath)}");
        var backupPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".emerald-backup-{Guid.NewGuid():N}{Path.GetExtension(targetPath)}");

        try
        {
            var result = TryCreateLinkOrCopy(sourcePath, tempTarget, preferredMode);
            File.Move(targetPath, backupPath);
            File.Move(tempTarget, targetPath);
            TryDeleteFile(backupPath);
            return result;
        }
        catch
        {
            TryDeleteFile(tempTarget);
            if (!File.Exists(targetPath) && File.Exists(backupPath))
            {
                File.Move(backupPath, targetPath);
            }

            throw;
        }
        finally
        {
            TryDeleteFile(backupPath);
        }
    }

    public bool AreOnSameRoot(string sourcePath, string targetPath)
    {
        var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
        var targetRoot = Path.GetPathRoot(Path.GetFullPath(targetPath));
        return !string.IsNullOrWhiteSpace(sourceRoot)
               && !string.IsNullOrWhiteSpace(targetRoot)
               && string.Equals(
                   sourceRoot,
                   targetRoot,
                   OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public bool IsSymbolicLink(string path)
    {
        try
        {
            return File.Exists(path)
                   && File.GetAttributes(path).HasFlag(System.IO.FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    public string? GetSymbolicLinkTarget(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private StoreLinkCreationResult TryCreateLinkOrCopy(string sourcePath, string targetPath, StoreLinkMode preferredMode)
    {
        if (preferredMode == StoreLinkMode.SymbolicLink)
        {
            try
            {
                File.CreateSymbolicLink(targetPath, sourcePath);
                return new StoreLinkCreationResult { LinkKind = StoreLinkKind.SymbolicLink };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                logger.LogWarning(ex, "Falling back to copy after symbolic link creation failed.");
                CopyFile(sourcePath, targetPath);
                return new StoreLinkCreationResult
                {
                    LinkKind = StoreLinkKind.Copy,
                    FallbackReason = ex.Message
                };
            }
        }

        if (preferredMode == StoreLinkMode.HardLink)
        {
            if (!AreOnSameRoot(sourcePath, targetPath))
            {
                CopyFile(sourcePath, targetPath);
                return new StoreLinkCreationResult
                {
                    LinkKind = StoreLinkKind.Copy,
                    FallbackReason = "Source and target are on different volumes."
                };
            }

            try
            {
                CreateHardLink(sourcePath, targetPath);
                return new StoreLinkCreationResult { LinkKind = StoreLinkKind.HardLink };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException or Win32Exception)
            {
                logger.LogWarning(ex, "Falling back to copy after hard link creation failed.");
                CopyFile(sourcePath, targetPath);
                return new StoreLinkCreationResult
                {
                    LinkKind = StoreLinkKind.Copy,
                    FallbackReason = ex.Message
                };
            }
        }

        CopyFile(sourcePath, targetPath);
        return new StoreLinkCreationResult { LinkKind = StoreLinkKind.Copy };
    }

    private static void CopyFile(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static void CreateHardLink(string sourcePath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!CreateHardLinkWindows(targetPath, sourcePath, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return;
        }

        if (link(sourcePath, targetPath) != 0)
        {
            throw new IOException($"Failed to create hard link. errno: {Marshal.GetLastPInvokeError()}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path) || File.GetAttributes(path).HasFlag(System.IO.FileAttributes.ReparsePoint))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkWindows(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);
}
