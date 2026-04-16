namespace Emerald.CoreX.Services;

public interface IJavaRuntimeCatalogService
{
    Task<IReadOnlyList<JavaRuntimeDescriptor>> DiscoverAsync(
        string? minecraftRootPath,
        IEnumerable<string>? savedPaths,
        CancellationToken cancellationToken = default);

    Task<JavaRuntimeValidationResult> ValidateAsync(
        string? candidatePath,
        CancellationToken cancellationToken = default);
}

public interface IJavaRuntimeProbe
{
    Task<JavaRuntimeProbeResult> ProbeAsync(string executablePath, CancellationToken cancellationToken = default);
}

public sealed class JavaRuntimeDescriptor
{
    public required string Path { get; init; }

    public required string DisplayPath { get; init; }

    public required string Source { get; init; }

    public string? Version { get; init; }

    public bool IsValid { get; init; }

    public bool IsCustomSaved { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class JavaRuntimeValidationResult
{
    public bool IsValid { get; init; }

    public string? NormalizedPath { get; init; }

    public string? ProbePath { get; init; }

    public string? Version { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class JavaRuntimeProbeResult
{
    public bool IsSuccess { get; init; }

    public string? Version { get; init; }

    public string? ErrorMessage { get; init; }
}
