namespace Emerald.CoreX.Services.Auth.Authlib;

public interface IAuthlibInjectorSettings
{
    AuthlibInjectorVersionMode VersionMode { get; }
    string? CustomVersion { get; }
}
