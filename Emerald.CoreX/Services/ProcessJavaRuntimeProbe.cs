using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed class ProcessJavaRuntimeProbe(ILogger<ProcessJavaRuntimeProbe> logger) : IJavaRuntimeProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public async Task<JavaRuntimeProbeResult> ProbeAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        var versionResult = await TryProbeAsync(executablePath, "--version", cancellationToken);
        if (versionResult.IsSuccess)
        {
            return versionResult;
        }

        var fallbackResult = await TryProbeAsync(executablePath, "-version", cancellationToken);
        if (fallbackResult.IsSuccess)
        {
            return fallbackResult;
        }

        return new JavaRuntimeProbeResult
        {
            IsSuccess = false,
            ErrorMessage = fallbackResult.ErrorMessage ?? versionResult.ErrorMessage ?? "Failed to execute Java version check."
        };
    }

    private async Task<JavaRuntimeProbeResult> TryProbeAsync(string executablePath, string argument, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = argument,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            if (!process.Start())
            {
                return new JavaRuntimeProbeResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to start '{executablePath}'."
                };
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ProbeTimeout);

            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var output = $"{stdout}\n{stderr}";
            var versionLine = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(versionLine))
            {
                return new JavaRuntimeProbeResult
                {
                    IsSuccess = true,
                    Version = versionLine
                };
            }

            return new JavaRuntimeProbeResult
            {
                IsSuccess = false,
                ErrorMessage = !string.IsNullOrWhiteSpace(versionLine)
                    ? versionLine
                    : $"'{Path.GetFileName(executablePath)} {argument}' exited with code {process.ExitCode}."
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new JavaRuntimeProbeResult
            {
                IsSuccess = false,
                ErrorMessage = $"Timed out while probing '{executablePath}'."
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to probe Java executable at {ExecutablePath} with argument {Argument}.", executablePath, argument);
            return new JavaRuntimeProbeResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
