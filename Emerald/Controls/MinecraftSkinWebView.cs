using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Emerald.Controls;

public enum MinecraftSkinViewerAnimation
{
    None,
    Idle,
    Walk,
    Run,
    Fly,
    Wave,
    Crouch,
    Hit,
    Swim
}

public enum MinecraftSkinViewerBackEquipment
{
    None,
    Cape,
    Elytra
}

public sealed record MinecraftSkinViewerLayer(bool Inner = true, bool Outer = true);

public sealed record MinecraftSkinViewerLayers(
    MinecraftSkinViewerLayer Head,
    MinecraftSkinViewerLayer Body,
    MinecraftSkinViewerLayer RightArm,
    MinecraftSkinViewerLayer LeftArm,
    MinecraftSkinViewerLayer RightLeg,
    MinecraftSkinViewerLayer LeftLeg)
{
    public static MinecraftSkinViewerLayers AllVisible { get; } = new(
        new MinecraftSkinViewerLayer(),
        new MinecraftSkinViewerLayer(),
        new MinecraftSkinViewerLayer(),
        new MinecraftSkinViewerLayer(),
        new MinecraftSkinViewerLayer(),
        new MinecraftSkinViewerLayer());
}

public sealed record MinecraftSkinViewerSettings(
    MinecraftSkinViewerAnimation Animation,
    double AnimationSpeed,
    bool AutoRotate,
    double AutoRotateSpeed,
    MinecraftSkinViewerBackEquipment BackEquipment,
    MinecraftSkinViewerLayers Layers,
    string? CapeUrl = null)
{
    public static MinecraftSkinViewerSettings Default { get; } = new(
        MinecraftSkinViewerAnimation.Idle,
        1d,
        true,
        Math.PI / 18d,
        MinecraftSkinViewerBackEquipment.None,
        MinecraftSkinViewerLayers.AllVisible,
        null);
}

/// <summary>
/// C# host for Emerald's packaged skinview3d page. All 3D rendering, animation,
/// layer handling, camera controls, and WebGL lifecycle are owned by skinview3d.
/// </summary>
public sealed class MinecraftSkinWebView : Grid
{
    private const string HtmlResourceName = "Emerald.SkinViewer.index.html";
    private const string BundleResourceName = "Emerald.SkinViewer.skinview3d.bundle.js";
    private const string DefaultCapeResourceName = "Emerald.SkinViewer.15th_Anniversary_Cape.png";
    private const string BundlePlaceholder = "/*__EMERALD_SKINVIEW3D_BUNDLE__*/";
    private static readonly TimeSpan ViewerLoadTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly Lazy<string> ViewerDocument = new(CreateViewerDocument);
    private static readonly Lazy<string> DefaultCapeDataUrl = new(CreateDefaultCapeDataUrl);

    public static string GetDefaultCapeDataUrl() => DefaultCapeDataUrl.Value;

    private readonly WebView2 _webView = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };
    private readonly TaskCompletionSource _readySource = NewCompletionSource();
    private TaskCompletionSource? _skinSource;
    private Task? _initializationTask;
    private bool _isStopped;

    public MinecraftSkinWebView()
    {
        Children.Add(_webView);
        _webView.NavigationCompleted += OnNavigationCompleted;
        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    public MinecraftSkinViewerSettings Settings { get; private set; } = MinecraftSkinViewerSettings.Default;

    public event Action<string>? ViewerFailed;

    public async Task SetSkinAsync(
        AccountSkinData skin,
        MinecraftSkinViewerSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isStopped, this);
        Settings = settings ?? Settings;

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(ViewerLoadTimeout);
        var loadCancellationToken = timeoutCancellation.Token;

        await EnsureInitializedAsync(loadCancellationToken);

        var completion = NewCompletionSource();
        _skinSource = completion;
        var state = new ViewerState(
            $"data:image/png;base64,{Convert.ToBase64String(skin.PngBytes)}",
            skin.Variant == MinecraftSkinVariant.Slim ? "slim" : "default",
            Settings);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        // WKWebView cannot marshal a JavaScript Promise (or undefined) as the
        // result of ExecuteScriptAsync. setState reports completion through the
        // WebMessageReceived bridge, so deliberately return a primitive here.
        await _webView.ExecuteScriptAsync($"(() => {{ window.emeraldSkinViewer.setState({json}); return 'started'; }})()");
        await completion.Task.WaitAsync(loadCancellationToken);
    }

    public async Task UpdateSettingsAsync(
        MinecraftSkinViewerSettings settings,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isStopped, this);
        Settings = settings;
        await _readySource.Task.WaitAsync(cancellationToken);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await _webView.ExecuteScriptAsync($"(() => {{ window.emeraldSkinViewer.applySettings({json}); return 'updated'; }})()");
    }

    /// <summary>
    /// Prevents callbacks and in-flight work from touching the native WebView while
    /// its ContentDialog host is being removed. The page's unload handler owns the
    /// JavaScript/WebGL cleanup.
    /// </summary>
    public async Task StopAsync()
    {
        if (_isStopped)
            return;

        _isStopped = true;
        try
        {
            if (_readySource.Task.IsCompletedSuccessfully)
                await _webView.ExecuteScriptAsync("(() => { window.emeraldSkinViewer?.dispose(); return 'stopped'; })()");
        }
        catch
        {
            // The native page may already be stopping. Managed teardown must still complete.
        }
        finally
        {
            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.WebMessageReceived -= OnWebMessageReceived;
            _readySource.TrySetCanceled();
            _skinSource?.TrySetCanceled();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        _initializationTask ??= InitializeCoreAsync();
        await _initializationTask.WaitAsync(cancellationToken);
        await _readySource.Task.WaitAsync(cancellationToken);
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await _webView.EnsureCoreWebView2Async();
            if (_isStopped)
                return;

            _webView.NavigateToString(ViewerDocument.Value);
        }
        catch (Exception exception)
        {
            Fail(exception.Message, exception);
            throw;
        }
    }

    private void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (_isStopped)
            return;

        if (!args.IsSuccess)
            Fail($"The embedded skin viewer could not be loaded ({args.WebErrorStatus}).");
    }

    private void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (_isStopped)
            return;

        try
        {
            var message = ParseMessage(args.WebMessageAsJson);
            switch (message.Type)
            {
                case "ready":
                    _readySource.TrySetResult();
                    break;
                case "skin-loaded":
                    _skinSource?.TrySetResult();
                    break;
                case "error":
                    Fail(message.Message ?? "skinview3d reported an unknown error.");
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.Message, exception);
        }
    }

    private void Fail(string message, Exception? exception = null)
    {
        if (_isStopped)
            return;

        var failure = exception ?? new InvalidOperationException(message);
        _readySource.TrySetException(failure);
        _skinSource?.TrySetException(failure);
        ViewerFailed?.Invoke(message);
    }

    private static ViewerMessage ParseMessage(string json)
    {
        using var outer = JsonDocument.Parse(json);
        if (outer.RootElement.ValueKind != JsonValueKind.String)
            return outer.RootElement.Deserialize<ViewerMessage>(JsonOptions) ?? new ViewerMessage(null, null);

        var innerJson = outer.RootElement.GetString();
        return string.IsNullOrEmpty(innerJson)
            ? new ViewerMessage(null, null)
            : JsonSerializer.Deserialize<ViewerMessage>(innerJson, JsonOptions) ?? new ViewerMessage(null, null);
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string CreateViewerDocument()
    {
        var html = ReadEmbeddedText(HtmlResourceName);
        var bundle = ReadEmbeddedText(BundleResourceName);
        if (!html.Contains(BundlePlaceholder, StringComparison.Ordinal))
            throw new InvalidOperationException("The embedded skin viewer template is missing its bundle placeholder.");

        return html.Replace(BundlePlaceholder, bundle, StringComparison.Ordinal);
    }

    private static string ReadEmbeddedText(string resourceName)
    {
        using var stream = typeof(MinecraftSkinWebView).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded skin viewer resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string CreateDefaultCapeDataUrl()
    {
        var assembly = typeof(MinecraftSkinWebView).Assembly;
        using var stream = assembly.GetManifestResourceStream(DefaultCapeResourceName)
            ?? assembly.GetManifestResourceStream("Emerald.Assets.Web.SkinViewer.15th_Anniversary_Cape.png")
            ?? throw new InvalidOperationException($"The embedded default cape resource '{DefaultCapeResourceName}' is missing.");
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return $"data:image/png;base64,{Convert.ToBase64String(memoryStream.ToArray())}";
    }

    private sealed record ViewerState(string Skin, string Model, MinecraftSkinViewerSettings Options);

    private sealed record ViewerMessage(string? Type, string? Message);
}
