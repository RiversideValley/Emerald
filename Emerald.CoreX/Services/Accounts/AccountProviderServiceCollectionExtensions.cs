using Emerald.CoreX.Services.Auth.Authlib;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth.Microsoft;
using Emerald.CoreX.Services.Auth.Offline;
using Emerald.CoreX.Services.Auth.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Auth;

public static class AccountProviderServiceCollectionExtensions
{
    /// <summary>Registers Emerald's built-in account-provider modules.</summary>
    public static IServiceCollection AddEmeraldAccountProviders(
        this IServiceCollection services,
        string microsoftClientId)
    {
        services.TryAddSingleton(new AccountProviderPolicyOptions());
        services.AddSingleton<IAccountProvider>(provider => new OfflineAccountProvider(
            provider.GetRequiredService<AccountProviderPolicyOptions>()));
        services.AddSingleton<IAccountProvider>(provider => new MicrosoftAccountProvider(
            new CmlLibMicrosoftAccountClient(provider.GetRequiredService<ILogger<AccountService>>()),
            microsoftClientId,
            provider.GetRequiredService<HttpClient>()));
        services.AddSingleton<IAccountProvider>(provider => new ElyByAccountProvider(
            provider.GetRequiredService<IElyByAccountStore>(),
            provider.GetRequiredService<IElyByAuthClient>(),
            provider.GetRequiredService<IBrowserOAuthBroker>(),
            provider.GetRequiredService<IAuthlibInjectorService>(),
            provider.GetRequiredService<ElyByOAuthOptions>(),
            provider.GetRequiredService<AccountProviderPolicyOptions>(),
            provider.GetRequiredService<ILogger<ElyByAccountProvider>>(),
            provider.GetRequiredService<HttpClient>()));
        return services;
    }
}
