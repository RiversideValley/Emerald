using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emerald.Services;

/// <summary>
/// Fallback for Uno 6.6.184, whose Desktop dispatcher catches queued callback
/// exceptions and reports them only through this exact logger category/message.
/// Uno's generated Application.UnhandledException handler remains enabled and is
/// Emerald's standard UI-exception path. This bridge must never turn arbitrary
/// framework errors into crashes.
///
/// Upgrade contract: when changing Uno, rerun dispatcher, async-void, and UI
/// unhandled-exception tests and confirm this log entry still includes an exception.
/// </summary>
internal sealed class NativeDispatcherFatalLoggerProvider(CrashCoordinator coordinator) : ILoggerProvider
{
    private static readonly object EarlyFactoryGate = new();
    private static BridgeLoggerFactory? _earlyFactory;

    /// <summary>
    /// Uno can cache its NativeDispatcher logger before the application host is
    /// built. Install a tiny early factory so that the pinned dispatcher bridge is
    /// present during that window. Cached loggers stay attached to this factory;
    /// the host is connected as a forwarding destination after Build().
    /// </summary>
    public static void InstallEarly(CrashCoordinator coordinator)
    {
        lock (EarlyFactoryGate)
        {
            if (_earlyFactory is not null)
            {
                return;
            }

            _earlyFactory = new BridgeLoggerFactory(coordinator);
            Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = _earlyFactory;
            // Uno.Foundation.Logging has its own adapter and caches loggers.
            // Setting AmbientLoggerFactory alone leaves framework loggers null.
            Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
        }
    }

    public static void AttachHost(ILoggerFactory hostFactory)
    {
        lock (EarlyFactoryGate)
        {
            if (_earlyFactory is null)
            {
                throw new InvalidOperationException("The early crash logger must be installed before the host.");
            }

            _earlyFactory.AttachHost(hostFactory);
            Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = _earlyFactory;
        }
    }

    public ILogger CreateLogger(string categoryName)
        => string.Equals(categoryName, "Uno.UI.Dispatching.NativeDispatcher", StringComparison.Ordinal)
            ? new NativeDispatcherLogger(coordinator)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class BridgeLoggerFactory(CrashCoordinator coordinator) : ILoggerFactory
    {
        private readonly NativeDispatcherFatalLoggerProvider _fatalProvider = new(coordinator);
        private ILoggerFactory? _host;

        public void AttachHost(ILoggerFactory host) => Volatile.Write(ref _host, host);
        public ILogger CreateLogger(string categoryName)
            => new BridgeLogger(this, categoryName, _fatalProvider.CreateLogger(categoryName));
        public void AddProvider(ILoggerProvider provider) => Volatile.Read(ref _host)?.AddProvider(provider);
        public void Dispose() { } // The host owns its own lifetime; this bridge is process-wide.

        private sealed class BridgeLogger(BridgeLoggerFactory owner, string category, ILogger fatal) : ILogger
        {
            private ILogger? _forward;
            private ILogger Forward
            {
                get
                {
                    if (Volatile.Read(ref _forward) is { } cached) return cached;
                    var host = Volatile.Read(ref owner._host);
                    if (host is null) return NullLogger.Instance;
                    var logger = host.CreateLogger(category);
                    return Interlocked.CompareExchange(ref _forward, logger, null) ?? logger;
                }
            }
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Forward.BeginScope(state);
            public bool IsEnabled(LogLevel level) => fatal.IsEnabled(level) || Forward.IsEnabled(level);
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                // This path is outside Serilog's provider filtering and sink error handling.
                fatal.Log(level, id, state, exception, formatter);
                Forward.Log(level, id, state, exception, formatter);
            }
        }
    }

    private sealed class NativeDispatcherLogger(CrashCoordinator coordinator) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Error || exception is null)
            {
                return;
            }

            string message;
            try
            {
                message = formatter(state, exception);
            }
            catch
            {
                return;
            }

            if (string.Equals(message, "NativeDispatcher unhandled exception", StringComparison.Ordinal))
            {
                coordinator.CaptureAndTerminate(exception, "Uno.NativeDispatcher");
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
