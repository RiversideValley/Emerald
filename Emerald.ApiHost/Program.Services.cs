using Emerald.ApiHost.Services;
using Emerald.CoreX;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Services.Auth.OAuth;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Emerald.ApiHost;

public partial class Program
{
    private static void ConfigureServices(IServiceCollection services, string basePath)
    {
        services.AddSingleton<IUiDispatcher, ThreadSafeUiDispatcher>();

        services.AddSingleton<IBaseSettingsService, BaseSettingsService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<BaseSettingsService>>();
            var path = Path.Combine(basePath, "settings");
            return new BaseSettingsService(logger, path);
        });
        services.AddSingleton<IMinecraftBaseSettingsService, MinecraftBaseSettingsService>();
        services.AddSingleton<IGlobalGameSettingsService, GlobalGameSettingsService>();
        services.AddSingleton<IGameRuntimeSettings, HeadlessGameRuntimeSettings>();
        services.AddSingleton<IJavaRuntimeProbe, ProcessJavaRuntimeProbe>();
        services.AddSingleton<IJavaRuntimeCatalogService, JavaRuntimeCatalogService>();
        services.AddSingleton(_ =>
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Emerald-Launcher/1.0");
            return client;
        });
        services.AddSingleton<INetworkCapabilityService, NetworkCapabilityService>();
        services.AddSingleton<IDownloadActivityService, DownloadActivityService>();
        services.AddSingleton<DownloadTimeouts>();
        services.AddSingleton<IInstallationStateStore, InstallationStateStore>();
        services.AddSingleton<VerifiedGameInstaller>();
        services.AddSingleton<IInstanceInstallationService, InstanceInstallationService>();

        services.AddSingleton<ElyByOAuthOptions>(_ =>
            new ElyByOAuthOptions(
                GetBuildMetadata("Emerald.ElyByClientId"),
                GetBuildMetadata("Emerald.ElyByClientSecret"),
                GetBuildMetadata("Emerald.ElyByRedirectUri")));
        services.AddSingleton<IElyByAuthClient, ElyByAuthClient>();
        services.AddSingleton<IElyByAccountStore, ElyByAccountStore>();
        services.AddSingleton<ISystemBrowserLauncher, ProcessBrowserLauncher>();
        services.AddSingleton<IBrowserOAuthBroker>(provider =>
            new LoopbackBrowserOAuthBroker(
                provider.GetRequiredService<ILogger<LoopbackBrowserOAuthBroker>>(),
                provider.GetRequiredService<ISystemBrowserLauncher>()));

        services.AddSingleton<Emerald.CoreX.Services.Auth.Authlib.IAuthlibInjectorService>(provider =>
            new Emerald.CoreX.Services.Auth.Authlib.AuthlibInjectorService(
                provider.GetRequiredService<ILogger<Emerald.CoreX.Services.Auth.Authlib.AuthlibInjectorService>>(),
                Path.Combine(basePath, "authlib-injector")));

        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton(new AccountProviderPolicyOptions
        {
            RequireMicrosoftForOfflineAccounts = true,
            RequireMicrosoftForElyByAccounts = true
        });
        services.AddEmeraldAccountProviders(GetBuildMetadata("Emerald.MSFTClientId"));

        services.AddSingleton<IAccountService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<AccountService>>();
            var settings = provider.GetRequiredService<IBaseSettingsService>();
            var dispatcher = provider.GetRequiredService<IUiDispatcher>();
            var accountsFile = Path.Combine(basePath, "accounts", "cml_accounts.json");

            return new AccountService(
                logger,
                settings,
                dispatcher,
                provider.GetServices<IAccountProvider>(),
                accountsFile,
                notificationService: provider.GetRequiredService<INotificationService>());
        });

        services.AddSingleton<IGameRuntimeService, GameRuntimeService>();

        services.AddTransient<IModLoaderInstaller, Fabric>();
        services.AddTransient<IModLoaderInstaller, Forge>();
        services.AddTransient<IModLoaderInstaller, NeoForge>();
        services.AddTransient<IModLoaderInstaller, LiteLoader>();
        services.AddTransient<IModLoaderInstaller, Quilt>();
        services.AddTransient<IModLoaderInstaller, Optifine>();
        services.AddTransient<ModLoaderRouter>();

        services.AddTransient<ModStore>();
        services.AddTransient<ResourcePackStore>();
        services.AddTransient<ShaderStore>();
        services.AddTransient<DataPackStore>();
        services.AddTransient<ModPackStore>();
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ModStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ResourcePackStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ShaderStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<DataPackStore>());
        services.AddTransient<IModrinthStore>(provider => provider.GetRequiredService<ModPackStore>());
        services.AddSingleton<IStoreFileLinkService, StoreFileLinkService>();
        services.AddSingleton<IStoreInstallRecordRepository, StoreInstallRecordRepository>();
        services.AddSingleton<IStoreSharedContentSettingsService, StoreSharedContentSettingsService>();
        services.AddSingleton<IStoreSharedContentService, StoreSharedContentService>();
        services.AddTransient<IGameStoreContentService, GameStoreContentService>();
        services.AddTransient<Emerald.CoreX.Modpacks.IMrPackReader, Emerald.CoreX.Modpacks.MrPackReader>();
        services.AddTransient<Emerald.CoreX.Modpacks.IMrPackFileInstaller, Emerald.CoreX.Modpacks.MrPackFileInstaller>();
        services.AddTransient<Emerald.CoreX.Modpacks.IModpackInstanceCreationService, Emerald.CoreX.Modpacks.ModpackInstanceCreationService>();

        services.AddSingleton<Core>();
        services.AddSingleton<EventHub>();
    }

    private static string GetBuildMetadata(string key)
        => typeof(Program).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value
           ?? string.Empty;
}
