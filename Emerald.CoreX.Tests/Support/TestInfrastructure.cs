using System.Collections.Concurrent;
using CmlLib.Core.Auth;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
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

    public void Set<T>(string key, T value)
    {
        _values[key] = value;
        SetCount++;
    }

    public T Get<T>(string key, T defaultVal)
        => _values.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : defaultVal;

    public T? Peek<T>(string key)
        => _values.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : default;
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

    public Func<FakeMicrosoftAccountClient, Task<MicrosoftInteractiveSignInResult>>? OnInteractiveSignInAsync { get; set; }
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

    public async Task<MicrosoftInteractiveSignInResult> SignInInteractivelyAsync()
    {
        if (OnInteractiveSignInAsync is null)
        {
            return new MicrosoftInteractiveSignInResult(DefaultAccountIdentifier, null, null);
        }

        return await OnInteractiveSignInAsync(this);
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
