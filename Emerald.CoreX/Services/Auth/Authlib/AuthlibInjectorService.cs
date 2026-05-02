using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Auth.Authlib;

public sealed class AuthlibInjectorService : IAuthlibInjectorService
{
    private const string Version = "1.2.7";
    private const string FileName = $"authlib-injector-{Version}.jar";
    private const string DownloadUrl = $"https://github.com/yushijinhun/authlib-injector/releases/download/v{Version}/{FileName}";

    private readonly ILogger<AuthlibInjectorService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _baseDirectory;

    public AuthlibInjectorService(
        ILogger<AuthlibInjectorService> logger,
        string? baseDirectory = null,
        HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald", "authlib-injector")
            : baseDirectory;
    }

    public async Task<string> GetJavaAgentArgumentAsync(CancellationToken cancellationToken = default)
    {
        var jarPath = await EnsureJarAsync(cancellationToken).ConfigureAwait(false);
        return $"-javaagent:{jarPath}=ely.by";
    }

    private async Task<string> EnsureJarAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_baseDirectory);

        var jarPath = Path.Combine(_baseDirectory, FileName);
        if (File.Exists(jarPath))
            return jarPath;

        var tempPath = jarPath + ".tmp";
        _logger.LogInformation("Downloading authlib-injector {Version} to {Path}.", Version, jarPath);

        using var response = await _httpClient
            .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var destination = File.Create(tempPath))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(jarPath))
            File.Delete(jarPath);

        File.Move(tempPath, jarPath);
        return jarPath;
    }
}
