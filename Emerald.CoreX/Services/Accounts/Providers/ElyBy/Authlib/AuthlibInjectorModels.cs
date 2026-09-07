namespace Emerald.CoreX.Services.Auth.Authlib;

public sealed record AuthlibInjectorVersion(int BuildNumber, string Version);

public sealed record AuthlibInjectorLaunchConfiguration(
    string Version,
    int BuildNumber,
    string JarPath,
    Uri ApiRoot,
    IReadOnlyList<string> JvmArguments,
    bool UsedCachedDescriptor);
