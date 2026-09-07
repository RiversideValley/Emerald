using Emerald.CoreX.Services.Auth.Authlib;

namespace Emerald.Services;

public sealed class AuthlibInjectorSettingsAdapter(SettingsService settingsService) : IAuthlibInjectorSettings
{
    public AuthlibInjectorVersionMode VersionMode
        => settingsService.Settings.App.Advanced.AuthlibInjector.VersionMode;

    public string? CustomVersion
        => settingsService.Settings.App.Advanced.AuthlibInjector.CustomVersion;
}
