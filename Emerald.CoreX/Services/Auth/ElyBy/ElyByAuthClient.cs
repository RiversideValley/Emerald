using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Auth.ElyBy;

internal sealed class ElyByAuthClient : IElyByAuthClient
{
    private static readonly Uri BaseUri = new("https://authserver.ely.by/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<ElyByAuthClient> _logger;
    private readonly HttpClient _httpClient;

    public ElyByAuthClient(ILogger<ElyByAuthClient> logger, HttpClient? httpClient = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= BaseUri;
    }

    public async Task<ElyByAuthSession> AuthenticateAsync(
        string login,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var clientToken = Guid.NewGuid().ToString("N");

        try
        {
            return await AuthenticateCoreAsync(login, password, clientToken, cancellationToken).ConfigureAwait(false);
        }
        catch (ElyByTwoFactorRequiredException) when (!string.IsNullOrWhiteSpace(twoFactorCode))
        {
            return await AuthenticateCoreAsync(login, $"{password}:{twoFactorCode}", clientToken, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ValidateAsync(
        string accessToken,
        string clientToken,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
                "auth/validate",
                new ElyByTokenRequest(accessToken, clientToken),
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent)
            return true;

        if (response.IsSuccessStatusCode)
            return true;

        _logger.LogDebug("Ely.by token validation failed with status {StatusCode}.", response.StatusCode);
        return false;
    }

    public async Task<ElyByAuthSession> RefreshAsync(
        ElyByStoredAccount account,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ElyByTokenRequest, ElyByAuthResponse>(
                "auth/refresh",
                new ElyByTokenRequest(account.AccessToken, account.ClientToken),
                cancellationToken)
            .ConfigureAwait(false);

        return CreateSession(response, account.ClientToken, account.Name, account.UUID);
    }

    public async Task InvalidateAsync(
        ElyByStoredAccount account,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
                "auth/invalidate",
                new ElyByTokenRequest(account.AccessToken, account.ClientToken),
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
        {
            _logger.LogWarning(
                "Ely.by token invalidation for {AccountName} returned {StatusCode}.",
                account.Name,
                response.StatusCode);
        }
    }

    private async Task<ElyByAuthSession> AuthenticateCoreAsync(
        string login,
        string password,
        string clientToken,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<ElyByAuthenticateRequest, ElyByAuthResponse>(
                "auth/authenticate",
                new ElyByAuthenticateRequest(login, password, clientToken, true),
                cancellationToken)
            .ConfigureAwait(false);

        return CreateSession(response, clientToken, null, null);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            return result ?? throw new ElyByAuthException("Ely.by returned an empty authentication response.");
        }

        var error = TryDeserializeError(body);
        if (IsTwoFactorRequired(error))
            throw new ElyByTwoFactorRequiredException();

        throw new ElyByAuthException(error?.ErrorMessage ?? error?.Error ?? $"Ely.by request failed with status {(int)response.StatusCode}.");
    }

    private static ElyByAuthSession CreateSession(
        ElyByAuthResponse response,
        string fallbackClientToken,
        string? fallbackName,
        string? fallbackUuid)
    {
        var name = response.SelectedProfile?.Name ?? fallbackName;
        var uuid = response.SelectedProfile?.Id ?? fallbackUuid;

        if (string.IsNullOrWhiteSpace(name))
            throw new ElyByAuthException("Ely.by returned an authentication response without a profile name.");

        if (string.IsNullOrWhiteSpace(uuid))
            throw new ElyByAuthException("Ely.by returned an authentication response without a profile id.");

        if (string.IsNullOrWhiteSpace(response.AccessToken))
            throw new ElyByAuthException("Ely.by returned an authentication response without an access token.");

        return new ElyByAuthSession(
            name,
            uuid,
            response.AccessToken,
            string.IsNullOrWhiteSpace(response.ClientToken) ? fallbackClientToken : response.ClientToken);
    }

    private static ElyByErrorResponse? TryDeserializeError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ElyByErrorResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTwoFactorRequired(ElyByErrorResponse? error)
        => error?.ErrorMessage?.Contains("two factor", StringComparison.OrdinalIgnoreCase) == true
           || error?.ErrorMessage?.Contains("2fa", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record ElyByAuthenticateRequest(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("clientToken")] string ClientToken,
        [property: JsonPropertyName("requestUser")] bool RequestUser);

    private sealed record ElyByTokenRequest(
        [property: JsonPropertyName("accessToken")] string AccessToken,
        [property: JsonPropertyName("clientToken")] string ClientToken);

    private sealed record ElyByAuthResponse(
        [property: JsonPropertyName("accessToken")] string? AccessToken,
        [property: JsonPropertyName("clientToken")] string? ClientToken,
        [property: JsonPropertyName("selectedProfile")] ElyByProfileResponse? SelectedProfile);

    private sealed class ElyByProfileResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed record ElyByErrorResponse(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("errorMessage")] string? ErrorMessage);
}
