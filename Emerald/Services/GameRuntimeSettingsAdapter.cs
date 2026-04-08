using Emerald.CoreX.Runtime;

namespace Emerald.Services;

public sealed class GameRuntimeSettingsAdapter(SettingsService settingsService) : IGameRuntimeSettings
{
    public bool IsLogCaptureEnabled
        => settingsService.Settings != null
            && settingsService.Settings.Minecraft != null
            && settingsService.Settings.Minecraft.JVM != null
            && settingsService.Settings.Minecraft.JVM.GameLogs;
}
