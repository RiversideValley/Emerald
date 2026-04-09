using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emerald.Services;

/// <summary>
/// Adapts persisted app settings into the runtime settings contract used by game launches.
/// </summary>
public sealed class GameRuntimeSettingsAdapter(SettingsService settingsService) : IGameRuntimeSettings
{
    private static ILogger Logger
    {
        get
        {
            try
            {
                return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(GameRuntimeSettingsAdapter).FullName!)
                    ?? NullLogger.Instance;
            }
            catch (InvalidOperationException)
            {
                return NullLogger.Instance;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether runtime log capture should be enabled for new sessions.
    /// </summary>
    public bool IsLogCaptureEnabled
    {
        get
        {
            var isEnabled = settingsService.Settings != null
                && settingsService.Settings.Minecraft != null
                && settingsService.Settings.Minecraft.JVM != null
                && settingsService.Settings.Minecraft.JVM.GameLogs;

            Logger.LogDebug(
                "Evaluated runtime log capture setting. IsEnabled: {IsEnabled}. HasSettings: {HasSettings}.",
                isEnabled,
                settingsService.Settings != null);

            return isEnabled;
        }
    }
}
