namespace Emerald.CoreX.Services.Auth.Authlib;

public interface IAuthlibInjectorService
{
    Task<AuthlibInjectorLaunchConfiguration> PrepareLaunchAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthlibInjectorVersion>>
        GetAvailableVersionsAsync(CancellationToken cancellationToken = default);
}
