using System.Diagnostics;

namespace Emerald.Services;

public static class PlatformFolderLauncher
{
    public static bool TryOpen(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            var startInfo = OperatingSystem.IsWindows()
                ? CreateWindowsStartInfo(path)
                : OperatingSystem.IsMacOS()
                    ? CreateUnixStartInfo("open", path)
                    : CreateUnixStartInfo("xdg-open", path);

            return Process.Start(startInfo) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo CreateWindowsStartInfo(string path)
    {
        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(path);
        return startInfo;
    }

    private static ProcessStartInfo CreateUnixStartInfo(string command, string path)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(path);
        return startInfo;
    }
}
