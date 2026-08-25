using System.Diagnostics;

namespace Emerald.CoreX.Helpers;

public static class FileManager
{
    /// <summary>
    /// Reveals a file or opens a folder in the platform's native file manager.
    /// Returns false silently if the path doesn't exist or the operation fails.
    /// </summary>
    public static bool Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            path = Path.GetFullPath(path);

            var isFile = File.Exists(path);
            var isDirectory = Directory.Exists(path);

            if (!isFile && !isDirectory)
                return false;

            if (OperatingSystem.IsWindows())
            {
                // Explorer:
                // file   -> open parent folder + select file
                // folder -> open folder
                return isFile
                    ? Start("explorer.exe", $"/select,{path}")
                    : Start("explorer.exe", path);
            }

            if (OperatingSystem.IsMacOS())
            {
                // Finder:
                // -R reveals/selects the item.
                return isFile
                    ? Start("/usr/bin/open", "-R", path)
                    : Start("/usr/bin/open", path);
            }

            if (OperatingSystem.IsLinux())
            {
                if (isDirectory)
                    return Start("xdg-open", path);

                // Try freedesktop FileManager1 first so the actual
                // file is selected instead of just opening its folder.
                if (TryLinuxReveal(path))
                    return true;

                // Fallback: open the containing directory.
                var directory = Path.GetDirectoryName(path);

                return directory is not null &&
                       Start("xdg-open", directory);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLinuxReveal(string path)
    {
        try
        {
            var uri = new Uri(path).AbsoluteUri;

            var info = CreateProcess("dbus-send");

            info.ArgumentList.Add("--session");
            info.ArgumentList.Add("--type=method_call");
            info.ArgumentList.Add("--print-reply=literal");
            info.ArgumentList.Add("--dest=org.freedesktop.FileManager1");
            info.ArgumentList.Add("/org/freedesktop/FileManager1");
            info.ArgumentList.Add("org.freedesktop.FileManager1.ShowItems");
            info.ArgumentList.Add($"array:string:{uri}");
            info.ArgumentList.Add("string:");

            using var process = Process.Start(info);

            if (process is null)
                return false;

            if (!process.WaitForExit(1000))
                return false;

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool Start(string fileName, params string[] args)
    {
        try
        {
            var info = CreateProcess(fileName);

            foreach (var arg in args)
                info.ArgumentList.Add(arg);

            using var process = Process.Start(info);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo CreateProcess(string fileName) => new()
    {
        FileName = fileName,

        // No shell/terminal involved.
        UseShellExecute = false,
        CreateNoWindow = true,

        // Prevent tools such as xdg-open/dbus-send from dumping
        // anything into the application's console.
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
}
