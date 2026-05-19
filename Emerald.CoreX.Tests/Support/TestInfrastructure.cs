using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CmlLib.Core.Auth;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth.Authlib;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth.Microsoft;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Support;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IocCollection : ICollectionFixture<IocFixture>
{
    public const string Name = "Ioc";
}

public sealed class IocFixture
{
    public IocFixture()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<INotificationService>(new NotificationService(NullLogger<NotificationService>.Instance));
        Ioc.Default.ConfigureServices(services.BuildServiceProvider());
    }
}

public sealed class InMemoryBaseSettingsService : IBaseSettingsService
{
    private readonly ConcurrentDictionary<string, object?> _values = new();

    public int SetCount { get; private set; }

    public void Set<T>(string key, T value, string? headerComment = null)
    {
        _values[key] = value;
        SetCount++;
    }

    public T Get<T>(string key, T defaultVal)
        => _values.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : defaultVal;

    public bool Exists(string key)
        => _values.ContainsKey(key);

    public void Delete(string key)
        => _values.TryRemove(key, out _);

    public T? Peek<T>(string key)
        => _values.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : default;
}

public sealed class InMemoryMinecraftBaseSettingsService : IMinecraftBaseSettingsService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object?>> _values = new();

    public string? CurrentBasePath { get; private set; }

    public bool IsInitialized => !string.IsNullOrWhiteSpace(CurrentBasePath);

    public void UseBasePath(string basePath)
    {
        CurrentBasePath = basePath;
        _values.GetOrAdd(basePath, _ => new ConcurrentDictionary<string, object?>());
    }

    public T Get<T>(string key, T defaultVal)
    {
        var values = CurrentValues;
        return values.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : defaultVal;
    }

    public void Set<T>(string key, T value)
        => CurrentValues[key] = value;

    public bool Exists(string key)
        => IsInitialized && CurrentValues.ContainsKey(key);

    public void Delete(string key)
    {
        if (IsInitialized)
        {
            CurrentValues.TryRemove(key, out _);
        }
    }

    public T? Peek<T>(string basePath, string key)
        => _values.TryGetValue(basePath, out var values)
           && values.TryGetValue(key, out var value)
           && value is T typedValue
            ? typedValue
            : default;

    private ConcurrentDictionary<string, object?> CurrentValues
        => IsInitialized && CurrentBasePath != null
            ? _values.GetOrAdd(CurrentBasePath, _ => new ConcurrentDictionary<string, object?>())
            : throw new InvalidOperationException("Minecraft base settings are not initialized.");
}

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool HasThreadAccess => true;

    public void Invoke(Action action) => action();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}

internal sealed class FakeMicrosoftAccountClient : IMicrosoftAccountClient
{
    public List<MicrosoftAccountInfo> Accounts { get; } = [];
    public List<string> AuthenticatedIdentifiers { get; } = [];
    public List<string> SignedOutIdentifiers { get; } = [];

    public string? InitializedClientId { get; private set; }
    public string? InitializedAccountStorePath { get; private set; }
    public string? DefaultAccountIdentifier { get; set; }

    public Func<FakeMicrosoftAccountClient, CancellationToken, Task<MicrosoftInteractiveSignInResult>>? OnInteractiveSignInAsync { get; set; }
    public Func<string, MSession>? AuthenticateFactory { get; set; }

    public Task InitializeAsync(string clientId, string accountStorePath)
    {
        InitializedClientId = clientId;
        InitializedAccountStorePath = accountStorePath;
        return Task.CompletedTask;
    }

    public IReadOnlyList<MicrosoftAccountInfo> GetAccounts()
        => Accounts.ToList();

    public string? GetDefaultAccountIdentifier()
        => DefaultAccountIdentifier;

    public async Task<MicrosoftInteractiveSignInResult> SignInInteractivelyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OnInteractiveSignInAsync is null)
        {
            return new MicrosoftInteractiveSignInResult(DefaultAccountIdentifier, null, null);
        }

        return await OnInteractiveSignInAsync(this, cancellationToken);
    }

    public Task<MSession> AuthenticateAsync(string accountIdentifier)
    {
        AuthenticatedIdentifiers.Add(accountIdentifier);
        var session = AuthenticateFactory?.Invoke(accountIdentifier)
            ?? MSession.CreateOfflineSession($"auth-{accountIdentifier}");
        return Task.FromResult(session);
    }

    public Task SignOutAsync(string accountIdentifier)
    {
        SignedOutIdentifiers.Add(accountIdentifier);
        Accounts.RemoveAll(account => string.Equals(account.Identifier, accountIdentifier, StringComparison.Ordinal));
        if (string.Equals(DefaultAccountIdentifier, accountIdentifier, StringComparison.Ordinal))
        {
            DefaultAccountIdentifier = Accounts
                .OrderByDescending(account => account.LastAccess)
                .Select(account => account.Identifier)
                .FirstOrDefault();
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeElyByAuthClient : IElyByAuthClient
{
    public ElyByAuthSession AuthenticateResult { get; set; } = new("ElyPlayer", "ely-uuid", "ely-access", "ely-client");
    public ElyByAuthSession ExchangeOAuthCodeResult { get; set; } = new(
        "ElyOAuthPlayer",
        "ely-oauth-uuid",
        "ely-oauth-access",
        "ely-oauth-client",
        "ely-oauth-refresh",
        DateTimeOffset.UtcNow.AddHours(1),
        ElyByAuthFlow.OAuth);
    public ElyByAuthSession RefreshResult { get; set; } = new("ElyPlayer", "ely-uuid", "ely-refreshed", "ely-client");
    public bool ValidateResult { get; set; } = true;

    public List<(string State, string? LoginHint)> AuthorizationRequestCalls { get; } = [];
    public List<string> ExchangeOAuthCodeCalls { get; } = [];
    public List<(string Login, string Password, string? TwoFactorCode)> AuthenticateCalls { get; } = [];
    public List<(string AccessToken, string ClientToken)> ValidateCalls { get; } = [];
    public List<string> RefreshCalls { get; } = [];
    public List<string> InvalidateCalls { get; } = [];

    public ElyByOAuthAuthorizationRequest CreateOAuthAuthorizationRequest(string state, string? loginHint = null)
    {
        AuthorizationRequestCalls.Add((state, loginHint));
        return new ElyByOAuthAuthorizationRequest(
            new Uri($"https://account.ely.by/oauth2/v1?state={state}"),
            new Uri("http://127.0.0.1:58135/oauth/elyby/"),
            state);
    }

    public Task<ElyByAuthSession> ExchangeOAuthCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExchangeOAuthCodeCalls.Add(code);
        return Task.FromResult(ExchangeOAuthCodeResult);
    }

    public Task<ElyByAuthSession> AuthenticateAsync(
        string login,
        string password,
        string? twoFactorCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuthenticateCalls.Add((login, password, twoFactorCode));
        return Task.FromResult(AuthenticateResult);
    }

    public Task<bool> ValidateAsync(string accessToken, string clientToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCalls.Add((accessToken, clientToken));
        return Task.FromResult(ValidateResult);
    }

    public Task<ElyByAuthSession> RefreshAsync(ElyByStoredAccount account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RefreshCalls.Add(account.UniqueId);
        return Task.FromResult(RefreshResult);
    }

    public Task InvalidateAsync(ElyByStoredAccount account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvalidateCalls.Add(account.UniqueId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeElyByOAuthBrowser : IElyByOAuthBrowser
{
    public string Code { get; set; } = "ely-oauth-code";
    public List<ElyByOAuthAuthorizationRequest> Requests { get; } = [];
    public Func<ElyByOAuthAuthorizationRequest, CancellationToken, Task<ElyByOAuthAuthorizationResult>>? OnAuthorizeAsync { get; set; }

    public Task<ElyByOAuthAuthorizationResult> AuthorizeAsync(
        ElyByOAuthAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        cancellationToken.ThrowIfCancellationRequested();

        return OnAuthorizeAsync is not null
            ? OnAuthorizeAsync(request, cancellationToken)
            : Task.FromResult(new ElyByOAuthAuthorizationResult(Code));
    }
}

internal sealed class FakeAuthlibInjectorService : IAuthlibInjectorService
{
    public string JavaAgentArgument { get; set; } = "-javaagent:/fake/authlib-injector.jar=ely.by";
    public int Calls { get; private set; }

    public Task<string> GetJavaAgentArgumentAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(JavaAgentArgument);
    }
}

internal sealed class FakeNotificationService : INotificationService
{
    public ObservableCollection<Notification> ActiveNotifications { get; } = [];
    public List<(string Title, string Message)> WarningCalls { get; } = [];
    public List<(string Title, string Message)> InfoCalls { get; } = [];
    public List<(string Title, string Message, Exception? Exception)> ErrorCalls { get; } = [];

    public (string Id, CancellationToken? CancellationToken) Create(
        string title,
        string message = null!,
        double progress = 0,
        bool isIndeterminate = false,
        bool isCancellable = false)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Message = message,
            Type = NotificationType.Progress,
            Progress = progress,
            IsIndeterminate = isIndeterminate,
            Timestamp = DateTime.UtcNow,
            CancellationSource = isCancellable ? new CancellationTokenSource() : null
        };

        ActiveNotifications.Add(notification);
        return (notification.Id, notification.CancellationSource?.Token);
    }

    public void Update(string? id = null, string? title = null, string? message = null, double? progress = null, bool? isIndeterminate = null)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        if (notification is null)
            return;

        if (title is not null)
            notification.Title = title;
        if (message is not null)
            notification.Message = message;
        if (progress is not null)
            notification.Progress = progress.Value;
        if (isIndeterminate is not null)
            notification.IsIndeterminate = isIndeterminate.Value;
    }

    public void Complete(string id, bool success, string message = null!, Exception ex = null!)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        if (notification is null)
            return;

        notification.Type = success ? NotificationType.Success : NotificationType.Error;
        notification.Message = message ?? notification.Message;
        if (ex is not null)
            notification.Exception = ex;
        notification.IsCompleted = true;
        notification.IsIndeterminate = false;
    }

    public string Warning(string title, string message, TimeSpan? duration = null)
    {
        WarningCalls.Add((title, message));
        return AddNotification(title, message, NotificationType.Warning, duration, null);
    }

    public string Info(string title, string message, TimeSpan? duration = null)
    {
        InfoCalls.Add((title, message));
        return AddNotification(title, message, NotificationType.Info, duration, null);
    }

    public string Error(string title, string message, TimeSpan? duration = null, Exception? ex = null)
    {
        ErrorCalls.Add((title, message, ex));
        return AddNotification(title, message, NotificationType.Error, duration, ex);
    }

    public void RemoveNotification(string id)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        if (notification is not null)
            ActiveNotifications.Remove(notification);
    }

    public void Cancel(string id)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        notification?.CancellationSource?.Cancel();
    }

    private string AddNotification(string title, string message, NotificationType type, TimeSpan? duration, Exception? ex)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.UtcNow,
            Duration = duration
        };
        if (ex is not null)
            notification.Exception = ex;

        ActiveNotifications.Add(notification);
        return notification.Id;
    }
}

public static class AsyncAssert
{
    public static async Task EventuallyAsync(Func<bool> condition, int timeoutMs = 2000, int pollMs = 25)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(pollMs);
        }

        Assert.True(condition());
    }
}
