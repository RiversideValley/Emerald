using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Services.Auth.OAuth;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Auth.ElyBy;

internal sealed class ElyByAuthClient : IElyByAuthClient
{
    private static readonly Uri AuthServerBaseUri = new("https://authserver.ely.by/");
    private static readonly Uri AccountBaseUri = new("https://account.ely.by/");
    private static readonly Uri OAuthTokenEndpoint = new(AccountBaseUri, "api/oauth2/v1/token");
    private static readonly Uri AccountInfoEndpoint = new(AccountBaseUri, "api/account/v1/info");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<ElyByAuthClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly ElyByOAuthOptions? _oauthOptions;

    public ElyByAuthClient(
        ILogger<ElyByAuthClient> logger,
        ElyByOAuthOptions? oauthOptions = null,
        HttpClient? httpClient = null)
    {
        _logger = logger;
        _oauthOptions = oauthOptions;
        _httpClient = httpClient ?? new HttpClient();
    }

    public BrowserOAuthAuthorizationRequest CreateOAuthAuthorizationRequest(string state, string? loginHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        var oauthOptions = GetConfiguredOAuthOptions();
        var redirectUri = new Uri(oauthOptions.RedirectUri, UriKind.Absolute);

        var parameters = new List<(string Key, string? Value)>
        {
            ("client_id", oauthOptions.ClientId),
            ("redirect_uri", oauthOptions.RedirectUri),
            ("response_type", "code"),
            ("scope", oauthOptions.Scope),
            ("prompt", "select_account"),
            ("state", state)
        };

        if (!string.IsNullOrWhiteSpace(loginHint))
            parameters.Add(("login_hint", loginHint));

        var authorizationUri = new Uri(AccountBaseUri, "oauth2/v1?" + BuildQuery(parameters));
        return new BrowserOAuthAuthorizationRequest("Ely.by", authorizationUri, redirectUri, state);
    }

    public async Task<ElyByAuthSession> ExchangeOAuthCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var oauthOptions = GetConfiguredOAuthOptions();
        ElyByOAuthTokenResponse response;
        try
        {
            response = await SendOAuthTokenRequestAsync(
                new Dictionary<string, string>
                {
                    ["client_id"] = oauthOptions.ClientId,
                    ["client_secret"] = oauthOptions.ClientSecret,
                    ["redirect_uri"] = oauthOptions.RedirectUri,
                    ["grant_type"] = "authorization_code",
                    ["code"] = code
                },
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ElyByAuthException ex) when (IsInvalidRefreshToken(ex.Message))
        {
            throw new ElyByReauthenticationRequiredException(
                "Your Ely.by sign-in has expired. Sign in again in your browser.", ex);
        }

        return await CreateOAuthSessionAsync(response, fallbackRefreshToken: null, cancellationToken).ConfigureAwait(false);
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
                new Uri(AuthServerBaseUri, "auth/validate"),
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
        if (account.AuthFlow == ElyByAuthFlow.OAuth && !string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            return await RefreshOAuthAsync(account, cancellationToken).ConfigureAwait(false);
        }

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
        if (account.AuthFlow == ElyByAuthFlow.OAuth)
        {
            _logger.LogDebug("Skipping remote Ely.by OAuth token invalidation for {AccountName}; no OAuth revoke endpoint is documented.", account.Name);
            return;
        }

        var response = await _httpClient.PostAsJsonAsync(
                new Uri(AuthServerBaseUri, "auth/invalidate"),
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
        using var response = await _httpClient.PostAsJsonAsync(new Uri(AuthServerBaseUri, path), request, JsonOptions, cancellationToken)
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

    private async Task<ElyByAuthSession> RefreshOAuthAsync(
        ElyByStoredAccount account,
        CancellationToken cancellationToken)
    {
        var oauthOptions = GetConfiguredOAuthOptions();
        var response = await SendOAuthTokenRequestAsync(
                new Dictionary<string, string>
                {
                    ["client_id"] = oauthOptions.ClientId,
                    ["client_secret"] = oauthOptions.ClientSecret,
                    ["scope"] = oauthOptions.Scope,
                    ["refresh_token"] = account.RefreshToken,
                    ["grant_type"] = "refresh_token"
                },
                cancellationToken)
            .ConfigureAwait(false);

        return await CreateOAuthSessionAsync(response, account.RefreshToken, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsInvalidRefreshToken(string message)
        => message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase)
           || message.Contains("invalid refresh", StringComparison.OrdinalIgnoreCase)
           || (message.Contains("refresh token", StringComparison.OrdinalIgnoreCase)
               && message.Contains("invalid", StringComparison.OrdinalIgnoreCase));

    private async Task<ElyByOAuthTokenResponse> SendOAuthTokenRequestAsync(
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(parameters);
        using var response = await _httpClient.PostAsync(OAuthTokenEndpoint, content, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = JsonSerializer.Deserialize<ElyByOAuthTokenResponse>(body, JsonOptions);
            return tokenResponse ?? throw new ElyByAuthException("Ely.by returned an empty OAuth token response.");
        }

        var error = TryDeserializeOAuthError(body);
        throw new ElyByAuthException(error?.ErrorDescription ?? error?.Error ?? $"Ely.by OAuth token request failed with status {(int)response.StatusCode}.");
    }

    private async Task<ElyByAuthSession> CreateOAuthSessionAsync(
        ElyByOAuthTokenResponse response,
        string? fallbackRefreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(response.AccessToken))
            throw new ElyByAuthException("Ely.by returned an OAuth response without an access token.");

        var accountInfo = await GetOAuthAccountInfoAsync(response.AccessToken, cancellationToken).ConfigureAwait(false);
        var uuid = NormalizeUuid(accountInfo.UUID);
        if (string.IsNullOrWhiteSpace(uuid))
            throw new ElyByAuthException("Ely.by returned account info without a UUID.");

        if (string.IsNullOrWhiteSpace(accountInfo.Username))
            throw new ElyByAuthException("Ely.by returned account info without a username.");

        var expiresAt = response.ExpiresIn > 0
            ? DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn)
            : (DateTimeOffset?)null;

        return new ElyByAuthSession(
            accountInfo.Username,
            uuid,
            response.AccessToken,
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(response.RefreshToken) ? fallbackRefreshToken : response.RefreshToken,
            expiresAt,
            ElyByAuthFlow.OAuth);
    }

    private async Task<ElyByAccountInfoResponse> GetOAuthAccountInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AccountInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var accountInfo = JsonSerializer.Deserialize<ElyByAccountInfoResponse>(body, JsonOptions);
            return accountInfo ?? throw new ElyByAuthException("Ely.by returned an empty account info response.");
        }

        var error = TryDeserializeOAuthAccountError(body);
        throw new ElyByAuthException(error?.Message ?? $"Ely.by account info request failed with status {(int)response.StatusCode}.");
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

    private ElyByOAuthOptions GetConfiguredOAuthOptions()
    {
        if (_oauthOptions is { IsConfigured: true })
            return _oauthOptions;

        throw new ElyByAuthException("Ely.by OAuth is not configured. Set the build-time client id, client secret, and redirect URI properties.");
    }

    private static string BuildQuery(IEnumerable<(string Key, string? Value)> parameters)
        => string.Join(
            "&",
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));

    private static string NormalizeUuid(string? uuid)
        => string.IsNullOrWhiteSpace(uuid) ? string.Empty : uuid.Replace("-", string.Empty, StringComparison.Ordinal);

    private static ElyByOAuthErrorResponse? TryDeserializeOAuthError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ElyByOAuthErrorResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ElyByOAuthAccountErrorResponse? TryDeserializeOAuthAccountError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ElyByOAuthAccountErrorResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

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

    private sealed record ElyByOAuthTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ElyByOAuthErrorResponse(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);

    private sealed record ElyByAccountInfoResponse(
        [property: JsonPropertyName("uuid")] string? UUID,
        [property: JsonPropertyName("username")] string? Username);

    private sealed record ElyByOAuthAccountErrorResponse(
        [property: JsonPropertyName("message")] string? Message);
}
