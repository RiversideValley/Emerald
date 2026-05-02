namespace Emerald.CoreX.Services.Auth.Authlib;

public interface IAuthlibInjectorService
{
    Task<string> GetJavaAgentArgumentAsync(CancellationToken cancellationToken = default);
}
