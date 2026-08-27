using System.Diagnostics;
using Emerald.CoreX.Services.Auth.OAuth;
using Microsoft.Extensions.Logging;

namespace Emerald.ApiHost.Services;

internal sealed class ProcessBrowserLauncher(ILogger<ProcessBrowserLauncher> logger) : ISystemBrowserLauncher
{
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var url = uri.AbsoluteUri;
            logger.LogInformation("Launching browser for account sign-in: {Url}", url);
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else if (OperatingSystem.IsLinux())
                Process.Start("xdg-open", url);
            else
                return Task.FromResult(false);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch browser for account sign-in.");
            return Task.FromResult(false);
        }
    }
}
