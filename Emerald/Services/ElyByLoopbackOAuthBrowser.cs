using System.Net;
using System.Text;
using Emerald.CoreX.Services.Auth.ElyBy;
using Microsoft.Extensions.Logging;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Emerald.Services;

internal sealed class ElyByLoopbackOAuthBrowser(
    ILogger<ElyByLoopbackOAuthBrowser> logger,
    DispatcherQueue dispatcherQueue) : IElyByOAuthBrowser
{
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(5);

    private readonly ILogger<ElyByLoopbackOAuthBrowser> _logger = logger;
    private readonly DispatcherQueue _dispatcherQueue = dispatcherQueue;

    public async Task<ElyByOAuthAuthorizationResult> AuthorizeAsync(
        ElyByOAuthAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureLoopbackRedirectUri(request.RedirectUri);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(AuthorizationTimeout);

        using var listener = new HttpListener();
        listener.Prefixes.Add(ToListenerPrefix(request.RedirectUri));
        listener.Start();

        var opened = await LaunchBrowserAsync(request.AuthorizationUri).ConfigureAwait(false);
        if (!opened)
            throw new ElyByAuthException("Could not open the Ely.by authorization page in your browser.");

        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeoutSource.Token).ConfigureAwait(false);
                if (!IsExpectedCallback(request.RedirectUri, context.Request.Url))
                {
                    await WriteHtmlResponseAsync(
                        context.Response,
                        404,
                        "Not found",
                        "This callback does not belong to the current Ely.by sign-in request.").ConfigureAwait(false);
                    continue;
                }

                var query = context.Request.QueryString;
                var state = query["state"];
                if (!string.Equals(state, request.State, StringComparison.Ordinal))
                {
                    await WriteHtmlResponseAsync(
                        context.Response,
                        400,
                        "Sign-in rejected",
                        "The Ely.by sign-in response did not match the original request.").ConfigureAwait(false);
                    throw new ElyByAuthException("Ely.by sign-in returned an invalid OAuth state.");
                }

                var error = query["error"];
                if (!string.IsNullOrWhiteSpace(error))
                {
                    var message = query["error_message"] ?? query["error_description"] ?? error;
                    await WriteHtmlResponseAsync(context.Response, 400, "Sign-in cancelled", message).ConfigureAwait(false);
                    throw new ElyByAuthException(message);
                }

                var code = query["code"];
                if (string.IsNullOrWhiteSpace(code))
                {
                    await WriteHtmlResponseAsync(
                        context.Response,
                        400,
                        "Sign-in failed",
                        "Ely.by did not return an authorization code.").ConfigureAwait(false);
                    throw new ElyByAuthException("Ely.by did not return an authorization code.");
                }

                await WriteHtmlResponseAsync(
                    context.Response,
                    200,
                    "Sign-in complete",
                    "You can close this browser tab and return to Emerald.").ConfigureAwait(false);
                return new ElyByOAuthAuthorizationResult(code);
            }
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

    private Task<bool> LaunchBrowserAsync(Uri uri)
    {
        if (_dispatcherQueue.HasThreadAccess)
            return Launcher.LaunchUriAsync(uri).AsTask();

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    completion.SetResult(await Launcher.LaunchUriAsync(uri));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("Failed to dispatch Ely.by browser launch to the UI thread."));
        }

        return completion.Task;
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
