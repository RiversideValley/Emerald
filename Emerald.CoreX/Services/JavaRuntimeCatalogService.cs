using CmlLib.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Emerald.CoreX.Services;

public sealed class JavaRuntimeCatalogService(
    IJavaRuntimeProbe probe,
    ILogger<JavaRuntimeCatalogService> logger) : IJavaRuntimeCatalogService
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public async Task<IReadOnlyList<JavaRuntimeDescriptor>> DiscoverAsync(
        string? minecraftRootPath,
        IEnumerable<string>? savedPaths,
        CancellationToken cancellationToken = default)
    {
        var candidates = new Dictionary<string, CandidateSeed>(PathComparer);

        foreach (var seed in GetCandidateSeeds(minecraftRootPath, savedPaths))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = NormalizeLaunchPath(seed.RawPath);
            if (string.IsNullOrWhiteSpace(normalized) || !File.Exists(normalized))
            {
                continue;
            }

            if (!candidates.ContainsKey(normalized))
            {
                candidates[normalized] = seed with { RawPath = normalized };
            }
            else if (seed.IsCustomSaved)
            {
                candidates[normalized] = candidates[normalized] with { IsCustomSaved = true };
            }
        }

        var results = new List<JavaRuntimeDescriptor>(candidates.Count);
        foreach (var candidate in candidates.Values.OrderByDescending(x => x.IsCustomSaved).ThenBy(x => x.Source).ThenBy(x => x.RawPath, PathComparer))
        {
            var validation = await ValidateNormalizedPathAsync(candidate.RawPath, cancellationToken);
            results.Add(new JavaRuntimeDescriptor
            {
                Path = candidate.RawPath,
                DisplayPath = candidate.RawPath,
                Source = candidate.Source,
                Version = validation.Version,
                IsValid = validation.IsValid,
                IsCustomSaved = candidate.IsCustomSaved,
                ErrorMessage = validation.ErrorMessage
            });
        }

        return results;
    }

    public Task<JavaRuntimeValidationResult> ValidateAsync(string? candidatePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeLaunchPath(candidatePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return Task.FromResult(new JavaRuntimeValidationResult
            {
                IsValid = false,
                ErrorMessage = "The selected path does not contain a Java executable."
            });
        }

        return ValidateNormalizedPathAsync(normalizedPath, cancellationToken);
    }

    private async Task<JavaRuntimeValidationResult> ValidateNormalizedPathAsync(string normalizedPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(normalizedPath))
        {
            return new JavaRuntimeValidationResult
            {
                IsValid = false,
                NormalizedPath = normalizedPath,
                ErrorMessage = $"Java executable not found at '{normalizedPath}'."
            };
        }

        var probePath = GetProbePath(normalizedPath);
        if (!File.Exists(probePath))
        {
            return new JavaRuntimeValidationResult
            {
                IsValid = false,
                NormalizedPath = normalizedPath,
                ProbePath = probePath,
                ErrorMessage = $"Java probe executable not found at '{probePath}'."
            };
        }

        var probeResult = await probe.ProbeAsync(probePath, cancellationToken);
        return new JavaRuntimeValidationResult
        {
            IsValid = probeResult.IsSuccess,
            NormalizedPath = normalizedPath,
            ProbePath = probePath,
            Version = probeResult.Version,
            ErrorMessage = probeResult.ErrorMessage
        };
    }

    private IEnumerable<CandidateSeed> GetCandidateSeeds(string? minecraftRootPath, IEnumerable<string>? savedPaths)
    {
        foreach (var path in GetMinecraftRuntimeCandidates(minecraftRootPath))
        {
            yield return path;
        }

        foreach (var path in GetEnvironmentCandidates())
        {
            yield return path;
        }

        foreach (var path in GetPlatformCandidates())
        {
            yield return path;
        }

        if (savedPaths == null)
        {
            yield break;
        }

        foreach (var savedPath in savedPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            yield return new CandidateSeed(savedPath, "Saved Path", true);
        }
    }

    private IEnumerable<CandidateSeed> GetMinecraftRuntimeCandidates(string? minecraftRootPath)
    {
        foreach (var root in new[]
                 {
                     minecraftRootPath,
                     MinecraftPath.GetOSDefaultPath()
                 }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(PathComparer))
        {
            string runtimePath;
            try
            {
                runtimePath = new MinecraftPath(root!).Runtime;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Skipping Minecraft runtime discovery for invalid root path {RootPath}.", root);
                continue;
            }

            foreach (var directory in EnumerateDirectoriesSafe(runtimePath))
            {
                yield return new CandidateSeed(directory.FullName, "Minecraft Runtime", false);
            }
        }
    }

    private IEnumerable<CandidateSeed> GetEnvironmentCandidates()
    {
        var javaHome = ExpandUserPath(Environment.GetEnvironmentVariable("JAVA_HOME"));
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            yield return new CandidateSeed(javaHome, "JAVA_HOME", false);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var expandedEntry = ExpandUserPath(entry);
            if (string.IsNullOrWhiteSpace(expandedEntry))
            {
                continue;
            }

            yield return new CandidateSeed(expandedEntry, "PATH", false);
        }
    }

    private IEnumerable<CandidateSeed> GetPlatformCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var path in GetWindowsCandidates())
            {
                yield return path;
            }

            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            foreach (var path in GetMacCandidates())
            {
                yield return path;
            }

            yield break;
        }

        foreach (var path in GetLinuxCandidates())
        {
            yield return path;
        }
    }

    private IEnumerable<CandidateSeed> GetWindowsCandidates()
    {
        foreach (var path in GetWindowsRegistryCandidates())
        {
            yield return new CandidateSeed(path, "Windows Registry", false);
        }

        foreach (var directory in GetWindowsProgramDirectories())
        {
            foreach (var child in EnumerateDirectoriesSafe(directory))
            {
                yield return new CandidateSeed(child.FullName, "Common Install Location", false);
            }
        }
    }

    private IEnumerable<string> GetWindowsRegistryCandidates()
    {
        var candidates = new HashSet<string>(PathComparer);
        var roots = new[]
        {
            Registry.LocalMachine,
            Registry.CurrentUser
        };

        var subKeys = new[]
        {
            @"SOFTWARE\JavaSoft\Java Runtime Environment",
            @"SOFTWARE\JavaSoft\Java Development Kit",
            @"SOFTWARE\WOW6432Node\JavaSoft\Java Runtime Environment",
            @"SOFTWARE\WOW6432Node\JavaSoft\Java Development Kit"
        };

        foreach (var root in roots)
        {
            foreach (var subKey in subKeys)
            {
                RegistryKey? baseKey = null;
                try
                {
                    baseKey = root.OpenSubKey(subKey);
                    if (baseKey == null)
                    {
                        continue;
                    }

                    foreach (var versionKeyName in baseKey.GetSubKeyNames())
                    {
                        using var versionKey = baseKey.OpenSubKey(versionKeyName);
                        var javaHome = versionKey?.GetValue("JavaHome") as string;
                        if (!string.IsNullOrWhiteSpace(javaHome))
                        {
                            candidates.Add(javaHome);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to read Java install candidates from registry key {RegistryKey}.", subKey);
                }
                finally
                {
                    baseKey?.Dispose();
                }
            }
        }

        return candidates;
    }

    private static IEnumerable<string> GetWindowsProgramDirectories()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(PathComparer);

        var vendorFolders = new[]
        {
            "Java",
            "Eclipse Adoptium",
            "AdoptOpenJDK",
            "Zulu",
            "BellSoft",
            "Microsoft"
        };

        foreach (var root in roots)
        {
            foreach (var vendorFolder in vendorFolders)
            {
                var directory = Path.Combine(root, vendorFolder);
                if (Directory.Exists(directory))
                {
                    yield return directory;
                }
            }
        }
    }

    private IEnumerable<CandidateSeed> GetMacCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var path in ExpandGlobLikeDirectories([
                     "/Library/Java/JavaVirtualMachines",
                     Path.Combine(home, "Library", "Java", "JavaVirtualMachines"),
                     "/opt/homebrew/opt",
                     "/usr/local/opt",
                     Path.Combine(home, ".sdkman", "candidates", "java")
                 ]))
        {
            yield return new CandidateSeed(path, "Common Install Location", false);
        }
    }

    private IEnumerable<CandidateSeed> GetLinuxCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var path in ExpandGlobLikeDirectories([
                     "/usr/lib/jvm",
                     "/usr/java",
                     Path.Combine(home, ".sdkman", "candidates", "java"),
                     Path.Combine(home, ".gradle", "jdks")
                 ]))
        {
            yield return new CandidateSeed(path, "Common Install Location", false);
        }

        foreach (var file in new[] { "/usr/bin/java", "/etc/alternatives/java" })
        {
            if (File.Exists(file))
            {
                yield return new CandidateSeed(file, "System Alternative", false);
            }
        }
    }

    private static IEnumerable<string> ExpandGlobLikeDirectories(IEnumerable<string> roots)
    {
        foreach (var root in roots.Select(ExpandUserPath).Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (Directory.Exists(root))
            {
                yield return root;

                foreach (var child in EnumerateDirectoriesSafe(root))
                {
                    yield return child.FullName;

                    var contentsHome = Path.Combine(child.FullName, "Contents", "Home");
                    if (Directory.Exists(contentsHome))
                    {
                        yield return contentsHome;
                    }

                    var libexecHome = Path.Combine(child.FullName, "libexec", "openjdk.jdk", "Contents", "Home");
                    if (Directory.Exists(libexecHome))
                    {
                        yield return libexecHome;
                    }
                }
            }
            else if (File.Exists(root))
            {
                yield return root;
            }
        }
    }

    private static IEnumerable<DirectoryInfo> EnumerateDirectoriesSafe(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return [];
        }

        try
        {
            return new DirectoryInfo(rootPath).EnumerateDirectories();
        }
        catch
        {
            return [];
        }
    }

    private static string? NormalizeLaunchPath(string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return null;
        }

        var expandedPath = ExpandUserPath(candidatePath);
        if (string.IsNullOrWhiteSpace(expandedPath))
        {
            return null;
        }

        try
        {
            expandedPath = Path.GetFullPath(expandedPath);
        }
        catch
        {
            return null;
        }

        if (File.Exists(expandedPath))
        {
            return NormalizeFilePath(expandedPath);
        }

        if (!Directory.Exists(expandedPath))
        {
            return null;
        }

        foreach (var filePath in GetLaunchFileCandidates(expandedPath))
        {
            if (File.Exists(filePath))
            {
                return NormalizeFilePath(filePath);
            }
        }

        return null;
    }

    private static string NormalizeFilePath(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && fileName.Equals("java.exe", StringComparison.OrdinalIgnoreCase))
            {
                var javawPath = Path.Combine(directory, "javaw.exe");
                if (File.Exists(javawPath))
                {
                    return javawPath;
                }
            }
        }

        return filePath;
    }

    private static IEnumerable<string> GetLaunchFileCandidates(string directoryPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return
            [
                Path.Combine(directoryPath, "javaw.exe"),
                Path.Combine(directoryPath, "java.exe"),
                Path.Combine(directoryPath, "bin", "javaw.exe"),
                Path.Combine(directoryPath, "bin", "java.exe")
            ];
        }

        return
        [
            Path.Combine(directoryPath, "java"),
            Path.Combine(directoryPath, "bin", "java")
        ];
    }

    private static string GetProbePath(string normalizedLaunchPath)
    {
        if (OperatingSystem.IsWindows()) 
        {
            var directory = Path.GetDirectoryName(normalizedLaunchPath);
            if (!string.IsNullOrWhiteSpace(directory) && Path.GetFileName(normalizedLaunchPath).Equals("javaw.exe", StringComparison.OrdinalIgnoreCase))
            {
                var javaExePath = Path.Combine(directory, "java.exe");
                if (File.Exists(javaExePath))
                {
                    return javaExePath;
                }
            }
        }

        return normalizedLaunchPath;
    }

    private static string? ExpandUserPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return path;
    }

    private readonly record struct CandidateSeed(string RawPath, string Source, bool IsCustomSaved);
}
