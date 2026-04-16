using Microsoft.Extensions.Logging.Abstractions;
using Emerald.CoreX.Services;
using Xunit;

namespace Emerald.CoreX.Tests.Services;

public sealed class JavaRuntimeCatalogServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "emerald-java-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidateAsync_NormalizesJavaHomeAndReturnsVersion()
    {
        var homePath = CreateFakeJavaHome("valid-home");
        var service = CreateService((_, _) => new JavaRuntimeProbeResult { IsSuccess = true, Version = "openjdk 21.0.2" });

        var result = await service.ValidateAsync(homePath);

        Assert.True(result.IsValid);
        Assert.Equal(GetExpectedLaunchPath(homePath), result.NormalizedPath);
        Assert.Equal("openjdk 21.0.2", result.Version);
    }

    [Fact]
    public async Task DiscoverAsync_DeduplicatesSameRuntimeAcrossInputs()
    {
        var homePath = CreateFakeJavaHome("duplicate-home");
        var expectedPath = GetExpectedLaunchPath(homePath);
        var service = CreateService((_, _) => new JavaRuntimeProbeResult { IsSuccess = true, Version = "openjdk 17" });

        var results = await service.DiscoverAsync(null, [homePath, expectedPath]);

        Assert.Single(results, runtime => PathsEqual(runtime.Path, expectedPath));
    }

    [Fact]
    public async Task DiscoverAsync_FiltersMissingSavedPaths()
    {
        var existingHome = CreateFakeJavaHome("existing-home");
        var missingHome = Path.Combine(_tempRoot, "missing-home");
        var service = CreateService((_, _) => new JavaRuntimeProbeResult { IsSuccess = true, Version = "openjdk 17" });

        var results = await service.DiscoverAsync(null, [existingHome, missingHome]);

        Assert.Contains(results, runtime => PathsEqual(runtime.Path, GetExpectedLaunchPath(existingHome)));
        Assert.DoesNotContain(results, runtime => PathsEqual(runtime.Path, GetExpectedLaunchPath(missingHome)));
    }

    [Fact]
    public async Task DiscoverAsync_KeepsInvalidRuntimeWithErrorDetails()
    {
        var invalidHome = CreateFakeJavaHome("invalid-home");
        var expectedPath = GetExpectedLaunchPath(invalidHome);
        var service = CreateService((path, _) => new JavaRuntimeProbeResult
        {
            IsSuccess = false,
            ErrorMessage = $"Probe failed for {Path.GetFileName(path)}"
        });

        var results = await service.DiscoverAsync(null, [invalidHome]);
        var invalidRuntime = Assert.Single(results, runtime => PathsEqual(runtime.Path, expectedPath));

        Assert.False(invalidRuntime.IsValid);
        Assert.Contains("Probe failed", invalidRuntime.ErrorMessage);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    private JavaRuntimeCatalogService CreateService(Func<string, CancellationToken, JavaRuntimeProbeResult> handler)
        => new(new FakeJavaRuntimeProbe(handler), NullLogger<JavaRuntimeCatalogService>.Instance);

    private string CreateFakeJavaHome(string name)
    {
        var home = Path.Combine(_tempRoot, name);
        var bin = Path.Combine(home, "bin");
        Directory.CreateDirectory(bin);

        foreach (var path in GetJavaFiles(bin))
        {
            File.WriteAllText(path, string.Empty);
        }

        return home;
    }

    private static IEnumerable<string> GetJavaFiles(string binPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return
            [
                Path.Combine(binPath, "java.exe"),
                Path.Combine(binPath, "javaw.exe")
            ];
        }

        return
        [
            Path.Combine(binPath, "java")
        ];
    }

    private static string GetExpectedLaunchPath(string homePath)
    {
        var binPath = Path.Combine(homePath, "bin");
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(binPath, "javaw.exe");
        }

        return Path.Combine(binPath, "java");
    }

    private static bool PathsEqual(string? left, string? right)
        => string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed class FakeJavaRuntimeProbe(Func<string, CancellationToken, JavaRuntimeProbeResult> handler) : IJavaRuntimeProbe
    {
        public Task<JavaRuntimeProbeResult> ProbeAsync(string executablePath, CancellationToken cancellationToken = default)
            => Task.FromResult(handler(executablePath, cancellationToken));
    }
}
