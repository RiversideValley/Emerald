using Emerald.ApiHost.Services;
using Emerald.CoreX;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Emerald.ApiHost;

public partial class Program
{
    const string ElyByClientId = "emerald1";
    const string ElyByClientSecret = "_hrxVlIoEWm1sqRlruFevD5v87mYW4EKPdmPWlraQoVP6kOXxJV9Y-qMrcm7Znk4";
    const string ElyByRedirectUri = "http://127.0.0.1:58135/oauth/elyby/";
    
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
                ElyByClientId,
                ElyByClientSecret,
                ElyByRedirectUri));
        services.AddSingleton<IElyByAuthClient, ElyByAuthClient>();
        services.AddSingleton<IElyByAccountStore, ElyByAccountStore>();
        services.AddSingleton<IElyByOAuthBrowser, HeadlessElyByOAuthBrowser>();

        services.AddSingleton<Emerald.CoreX.Services.Auth.Authlib.IAuthlibInjectorService>(provider =>
            new Emerald.CoreX.Services.Auth.Authlib.AuthlibInjectorService(
                provider.GetRequiredService<ILogger<Emerald.CoreX.Services.Auth.Authlib.AuthlibInjectorService>>(),
                Path.Combine(basePath, "authlib-injector")));

        services.AddSingleton<INotificationService, NotificationService>();

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
                accountsFile,
                notificationService: provider.GetRequiredService<INotificationService>(),
                elyByAuthClient: provider.GetRequiredService<IElyByAuthClient>(),
                elyByAccountStore: provider.GetRequiredService<IElyByAccountStore>(),
                elyByOAuthBrowser: provider.GetRequiredService<IElyByOAuthBrowser>(),
                authlibInjectorService: provider.GetRequiredService<Emerald.CoreX.Services.Auth.Authlib.IAuthlibInjectorService>());
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
}
