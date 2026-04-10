using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Notifications;
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
