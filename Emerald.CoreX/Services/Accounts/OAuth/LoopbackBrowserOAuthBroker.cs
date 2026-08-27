using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Auth.OAuth;

/// <summary>Reusable authorization-code broker for fixed HTTP loopback callbacks.</summary>
internal sealed class LoopbackBrowserOAuthBroker(
    ILogger<LoopbackBrowserOAuthBroker> logger,
    ISystemBrowserLauncher browserLauncher) : IBrowserOAuthBroker
{
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(5);

    public async Task<BrowserOAuthAuthorizationResult> AuthorizeAsync(
        BrowserOAuthAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Starting {Provider} browser OAuth callback on {RedirectUri}.", request.ProviderDisplayName, request.RedirectUri);
        ValidateRedirectUri(request.RedirectUri, request.ProviderDisplayName);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(AuthorizationTimeout);
        using var listener = StartListener(request);
        if (!await browserLauncher.OpenAsync(request.AuthorizationUri, cancellationToken).ConfigureAwait(false))
        {
            throw new BrowserOAuthException(
                BrowserOAuthFailureKind.BrowserLaunchFailed,
                $"Could not open the {request.ProviderDisplayName} authorization page in your browser.");
        }

        try
        {
            return await WaitForResultAsync(listener, request, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BrowserOAuthException(
                BrowserOAuthFailureKind.TimedOut,
                $"Timed out waiting for {request.ProviderDisplayName} browser sign-in to complete.");
        }
        catch (HttpListenerException ex) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new BrowserOAuthException(
                BrowserOAuthFailureKind.TimedOut,
                $"Timed out waiting for {request.ProviderDisplayName} browser sign-in to complete.",
                ex);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static HttpListener StartListener(BrowserOAuthAuthorizationRequest request)
    {
        var listener = new HttpListener();
        try
        {
            listener.Prefixes.Add(ToListenerPrefix(request.RedirectUri));
            listener.Start();
            return listener;
        }
        catch (HttpListenerException ex)
        {
            listener.Close();
            throw new BrowserOAuthException(
                BrowserOAuthFailureKind.CallbackPortUnavailable,
                $"{request.ProviderDisplayName} sign-in cannot start because callback port {request.RedirectUri.Port} is already in use. Close the app using that port and try again.",
                ex);
        }
    }

    private static async Task<BrowserOAuthAuthorizationResult> WaitForResultAsync(
        HttpListener listener,
        BrowserOAuthAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            var result = await TryHandleCallbackAsync(request, context).ConfigureAwait(false);
            if (result is not null)
                return result;
        }
    }

    private static async Task<BrowserOAuthAuthorizationResult?> TryHandleCallbackAsync(
        BrowserOAuthAuthorizationRequest request,
        HttpListenerContext context)
    {
        if (!IsExpectedCallback(request.RedirectUri, context.Request.Url))
        {
            await WriteResponseAsync(context.Response, 404, "Not found", "This callback does not belong to the current sign-in request.").ConfigureAwait(false);
            return null;
        }

        var state = context.Request.QueryString["state"];
        if (!IsExpectedState(state, request.State))
        {
            await WriteResponseAsync(context.Response, 400, "Sign-in rejected", "The response did not match the original request.").ConfigureAwait(false);
            throw new BrowserOAuthException(BrowserOAuthFailureKind.StateMismatch, $"{request.ProviderDisplayName} sign-in returned an invalid OAuth state.");
        }

        var error = context.Request.QueryString["error"];
        if (!string.IsNullOrWhiteSpace(error))
        {
            var message = context.Request.QueryString["error_message"]
                          ?? context.Request.QueryString["error_description"]
                          ?? error;
            await WriteResponseAsync(context.Response, 400, "Sign-in canceled", message).ConfigureAwait(false);
            throw new BrowserOAuthException(BrowserOAuthFailureKind.UserDenied, message);
        }

        var code = context.Request.QueryString["code"];
        if (string.IsNullOrWhiteSpace(code))
        {
            await WriteResponseAsync(context.Response, 400, "Sign-in failed", "The provider did not return an authorization code.").ConfigureAwait(false);
            throw new BrowserOAuthException(BrowserOAuthFailureKind.MalformedResponse, $"{request.ProviderDisplayName} did not return an authorization code.");
        }

        await WriteResponseAsync(context.Response, 200, "Sign-in complete", "You can close this browser tab and return to Emerald.").ConfigureAwait(false);
        return new BrowserOAuthAuthorizationResult(code);
    }

    private static bool IsExpectedState(string? actual, string expected)
    {
        if (actual is null)
            return false;

        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length
               && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static void ValidateRedirectUri(Uri redirectUri, string providerDisplayName)
    {
        if (!string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || !IPAddress.TryParse(redirectUri.Host, out var address)
            || !IPAddress.IsLoopback(address)
            || redirectUri.Port <= 0)
        {
            throw new BrowserOAuthException(
                BrowserOAuthFailureKind.InvalidRedirectUri,
                $"{providerDisplayName} OAuth requires an HTTP loopback redirect URI with a fixed callback port.");
        }
    }

    private static string ToListenerPrefix(Uri redirectUri)
    {
        var prefix = redirectUri.GetLeftPart(UriPartial.Path);
        return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : prefix + "/";
    }

    private static bool IsExpectedCallback(Uri expected, Uri? actual)
        => actual is not null
           && string.Equals(actual.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase)
           && string.Equals(actual.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
           && actual.Port == expected.Port
           && string.Equals(NormalizePath(actual.AbsolutePath), NormalizePath(expected.AbsolutePath), StringComparison.Ordinal);

    private static string NormalizePath(string path)
        => path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";

    private static async Task WriteResponseAsync(HttpListenerResponse response, int statusCode, string title, string body)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        var html = $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(title)}</title></head><body style=\"font-family:system-ui,sans-serif;margin:2rem\"><h1>{WebUtility.HtmlEncode(title)}</h1><p>{WebUtility.HtmlEncode(body)}</p></body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer).ConfigureAwait(false);
        response.Close();
    }
}
