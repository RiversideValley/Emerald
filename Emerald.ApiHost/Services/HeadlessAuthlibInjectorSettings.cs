using Emerald.CoreX.Services.Auth.Authlib;

namespace Emerald.ApiHost.Services;

internal sealed class HeadlessAuthlibInjectorSettings : IAuthlibInjectorSettings
{
    public AuthlibInjectorVersionMode VersionMode => AuthlibInjectorVersionMode.Recommended;
    public string? CustomVersion => null;
}
