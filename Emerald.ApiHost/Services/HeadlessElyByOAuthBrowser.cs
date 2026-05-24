using System.Diagnostics;
using System.Net;
using System.Text;
using Emerald.CoreX.Services.Auth.ElyBy;
using Microsoft.Extensions.Logging;

namespace Emerald.ApiHost.Services;

internal sealed class HeadlessElyByOAuthBrowser(
    ILogger<HeadlessElyByOAuthBrowser> logger) : IElyByOAuthBrowser
{
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(5);
    private readonly ILogger<HeadlessElyByOAuthBrowser> _logger = logger;

    public async Task<ElyByOAuthAuthorizationResult> AuthorizeAsync(
        ElyByOAuthAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureLoopbackRedirectUri(request.RedirectUri);

        using var timeoutSource = CreateAuthorizationTimeoutSource(cancellationToken);
        using var listener = StartLoopbackListener(request.RedirectUri);
        await EnsureBrowserOpenedAsync(request.AuthorizationUri).ConfigureAwait(false);

        try
        {
            return await WaitForAuthorizationResultAsync(listener, request, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ElyByAuthException("Timed out waiting for Ely.by browser sign-in to complete.");
        }
        catch (HttpListenerException ex) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new ElyByAuthException("Timed out waiting for Ely.by browser sign-in to complete.", ex);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static CancellationTokenSource CreateAuthorizationTimeoutSource(CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(AuthorizationTimeout);
        return timeoutSource;
    }

    private static HttpListener StartLoopbackListener(Uri redirectUri)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(ToListenerPrefix(redirectUri));
        listener.Start();
        return listener;
    }

    private async Task EnsureBrowserOpenedAsync(Uri authorizationUri)
    {
        var opened = await LaunchBrowserAsync(authorizationUri).ConfigureAwait(false);
        if (!opened)
            throw new ElyByAuthException("Could not open the Ely.by authorization page in your browser.");
    }

    private static async Task<ElyByOAuthAuthorizationResult> WaitForAuthorizationResultAsync(
        HttpListener listener,
        ElyByOAuthAuthorizationRequest request,
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

    private static async Task<ElyByOAuthAuthorizationResult?> TryHandleCallbackAsync(
        ElyByOAuthAuthorizationRequest request,
        HttpListenerContext context)
    {
        if (!IsExpectedCallback(request.RedirectUri, context.Request.Url))
        {
            await WriteHtmlResponseAsync(
                context.Response,
                404,
                "Not found",
                "This callback does not belong to the current Ely.by sign-in request.").ConfigureAwait(false);
            return null;
        }

        await EnsureExpectedStateAsync(request, context).ConfigureAwait(false);
        await EnsureNoOAuthErrorAsync(context).ConfigureAwait(false);
        var code = await GetAuthorizationCodeAsync(context).ConfigureAwait(false);

        await WriteHtmlResponseAsync(
            context.Response,
            200,
            "Sign-in complete",
            "You can close this browser tab and return to Emerald.").ConfigureAwait(false);

        return new ElyByOAuthAuthorizationResult(code);
    }

    private static async Task EnsureExpectedStateAsync(
        ElyByOAuthAuthorizationRequest request,
        HttpListenerContext context)
    {
        var state = context.Request.QueryString["state"];
        if (string.Equals(state, request.State, StringComparison.Ordinal))
            return;

        await WriteHtmlResponseAsync(
            context.Response,
            400,
            "Sign-in rejected",
            "The Ely.by sign-in response did not match the original request.").ConfigureAwait(false);
        throw new ElyByAuthException("Ely.by sign-in returned an invalid OAuth state.");
    }

    private static async Task EnsureNoOAuthErrorAsync(HttpListenerContext context)
    {
        var query = context.Request.QueryString;
        var error = query["error"];
        if (string.IsNullOrWhiteSpace(error))
            return;

        var message = query["error_message"] ?? query["error_description"] ?? error;
        await WriteHtmlResponseAsync(context.Response, 400, "Sign-in cancelled", message).ConfigureAwait(false);
        throw new ElyByAuthException(message);
    }

    private static async Task<string> GetAuthorizationCodeAsync(HttpListenerContext context)
    {
        var code = context.Request.QueryString["code"];
        if (!string.IsNullOrWhiteSpace(code))
            return code;

        await WriteHtmlResponseAsync(
            context.Response,
            400,
            "Sign-in failed",
            "Ely.by did not return an authorization code.").ConfigureAwait(false);
        throw new ElyByAuthException("Ely.by did not return an authorization code.");
    }

    private Task<bool> LaunchBrowserAsync(Uri uri)
    {
        try
        {
            var url = uri.AbsoluteUri;
            _logger.LogInformation("Launching browser for Ely.by sign-in: {Url}", url);

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
            else
            {
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch browser for Ely.by authorization");
            return Task.FromResult(false);
        }
    }

    private static void EnsureLoopbackRedirectUri(Uri redirectUri)
    {
        if (!string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            throw new ElyByAuthException("Ely.by OAuth redirect URI must use http for the local loopback callback.");

        if (!IPAddress.TryParse(redirectUri.Host, out var address) || !IPAddress.IsLoopback(address))
            throw new ElyByAuthException("Ely.by OAuth redirect URI must use a loopback host such as 127.0.0.1.");

        if (redirectUri.Port <= 0)
            throw new ElyByAuthException("Ely.by OAuth redirect URI must include a local callback port.");
    }

    private static string ToListenerPrefix(Uri redirectUri)
    {
        var prefix = redirectUri.GetLeftPart(UriPartial.Path);
        return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : prefix + "/";
    }

    private static bool IsExpectedCallback(Uri expectedRedirectUri, Uri? actualUri)
    {
        if (actualUri is null)
            return false;

        return string.Equals(actualUri.Scheme, expectedRedirectUri.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(actualUri.Host, expectedRedirectUri.Host, StringComparison.OrdinalIgnoreCase)
               && actualUri.Port == expectedRedirectUri.Port
               && string.Equals(
                   NormalizePath(actualUri.AbsolutePath),
                   NormalizePath(expectedRedirectUri.AbsolutePath),
                   StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
        => path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";

    private static async Task WriteHtmlResponseAsync(
        HttpListenerResponse response,
        int statusCode,
        string title,
        string body)
    {
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        var html = $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>{WebUtility.HtmlEncode(title)}</title>
            </head>
            <body style="font-family: system-ui, sans-serif; margin: 2rem;">
              <h1>{WebUtility.HtmlEncode(title)}</h1>
              <p>{WebUtility.HtmlEncode(body)}</p>
            </body>
            </html>
            """;
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer).ConfigureAwait(false);
        response.Close();
    }
}
