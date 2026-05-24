using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CmlLib.Core;
using Emerald.ApiHost.Services;
using Emerald.CoreX;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Emerald.ApiHost;

public class Program
{
    public static void Main(string[] args)
    {
        var basePath = "";
        var port = 58135;
        string? socketPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--base-path" || args[i] == "-b") && i + 1 < args.Length)
            {
                basePath = args[i + 1];
                i++;
            }
            else if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedPort))
            {
                port = parsedPort;
                i++;
            }
            else if ((args[i] == "--socket" || args[i] == "-s") && i + 1 < args.Length)
            {
                socketPath = args[i + 1];
                i++;
            }
        }

        if (string.IsNullOrEmpty(basePath))
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald");
        }

        basePath = Path.GetFullPath(basePath);
        Directory.CreateDirectory(basePath);

        var builder = WebApplication.CreateBuilder(args);

        // Configure Kestrel Transport
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (!string.IsNullOrEmpty(socketPath))
            {
                var fullSocketPath = Path.GetFullPath(socketPath);
                if (File.Exists(fullSocketPath))
                {
                    File.Delete(fullSocketPath);
                }
                options.ListenUnixSocket(fullSocketPath);
            }
            else
            {
                options.ListenLocalhost(port);
            }
        });

        // Configure base logging
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
        });

        ConfigureServices(builder.Services, basePath);

        var app = builder.Build();

        ConfigureApp(app, basePath);

        app.Run();
    }

    private static void ConfigureServices(IServiceCollection services, string basePath)
    {
        // 1. Thread Dispatcher
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();

        // 2. Settings Providers
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

        // 3. ElyBy Authentication
        const string ElyByClientId = "emerald1";
        const string ElyByClientSecret = "_hrxVlIoEWm1sqRlruFevD5v87mYW4EKPdmPWlraQoVP6kOXxJV9Y-qMrcm7Znk4";
        const string ElyByRedirectUri = "http://127.0.0.1:58135/oauth/elyby/";

        services.AddSingleton<ElyByOAuthOptions>(_ =>
            new ElyByOAuthOptions(
                ElyByClientId,
                ElyByClientSecret,
                ElyByRedirectUri));
        services.AddSingleton<IElyByAuthClient, ElyByAuthClient>();
        services.AddSingleton<IElyByAccountStore, ElyByAccountStore>();
        services.AddSingleton<IElyByOAuthBrowser, HeadlessElyByOAuthBrowser>();

        // 4. Authlib Injector
        services.AddSingleton<Emerald.CoreX.Services.Auth.Authlib.IAuthlibInjectorService>(provider =>
            new Emerald.CoreX.Services.Auth.Authlib.AuthlibInjectorService(
                provider.GetRequiredService<ILogger<Emerald.CoreX.Services.Auth.Authlib.AuthlibInjectorService>>(),
                Path.Combine(basePath, "authlib-injector")));

        // 5. Notifications Service
        services.AddSingleton<INotificationService, NotificationService>();

        // 6. Accounts Coordinator
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

        // 7. Game Runtime Service
        services.AddSingleton<IGameRuntimeService, GameRuntimeService>();

        // 8. Mod Loader Installers
        services.AddTransient<IModLoaderInstaller, Fabric>();
        services.AddTransient<IModLoaderInstaller, Forge>();
        services.AddTransient<IModLoaderInstaller, NeoForge>();
        services.AddTransient<IModLoaderInstaller, LiteLoader>();
        services.AddTransient<IModLoaderInstaller, Quilt>();
        services.AddTransient<IModLoaderInstaller, Optifine>();
        services.AddTransient<ModLoaderRouter>();

        // 9. Store Integrations
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

        // 10. Main Coordinator Core
        services.AddSingleton<Core>();

        // 11. WebSocket Event Hub
        services.AddSingleton<EventHub>();
    }

    private static void ConfigureApp(WebApplication app, string basePath)
    {
        // Enable WebSockets
        app.UseWebSockets();

        // Set up WebSocket Handler
        app.Map("/ws/events", async (HttpContext context, EventHub eventHub) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await eventHub.HandleSocketAsync(webSocket);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        });

        // Initialize the Core Engine on Startup
        var core = app.Services.GetRequiredService<Core>();
        var accountService = app.Services.GetRequiredService<IAccountService>();

        // Trigger Async Core Initialization
        _ = Task.Run(async () =>
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            try
            {
                logger.LogInformation("Initializing Emerald CoreX Headless engine...");
                
                // Setup Account Service
                const string MicrosoftClientId = "dfeccda7-604a-4895-b409-9d35f1679b5d";
                await accountService.InitializeAsync(MicrosoftClientId);
                
                // Initialize Core Coordinator
                var minecraftPath = new MinecraftPath(basePath);
                await core.InitializeAndRefresh(minecraftPath);

                logger.LogInformation("Emerald CoreX Headless engine initialized successfully! Minecraft base path: {Path}", basePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Emerald CoreX Headless engine on startup.");
            }
        });

        // --- API ROUTES ---

        // 1. Accounts Endpoints
        app.MapGet("/api/accounts", (IAccountService ac) =>
            ac.Accounts.Select(a => new
            {
                Identifier = a.UniqueId,
                Username = a.Name,
                Type = a.Type.ToString(),
                LastAccess = a.LastUsed
            }));

        app.MapGet("/api/accounts/selected", (IAccountService ac) =>
        {
            var selected = ac.GetSelectedAccount();
            if (selected == null) return Results.NotFound(new { Error = "No account is currently selected." });
            return Results.Ok(new
            {
                Identifier = selected.UniqueId,
                Username = selected.Name,
                Type = selected.Type.ToString(),
                LastAccess = selected.LastUsed
            });
        });

        app.MapPost("/api/accounts/select", (string identifier, IAccountService ac) =>
        {
            var account = ac.Accounts.FirstOrDefault(a => a.UniqueId == identifier);
            if (account == null) return Results.NotFound(new { Error = $"Account with identifier '{identifier}' not found." });
            ac.SetSelectedAccount(account);
            return Results.Ok(new { Message = "Account selected successfully.", Username = account.Name });
        });

        app.MapPost("/api/accounts/offline", (string username, IAccountService ac) =>
        {
            if (string.IsNullOrWhiteSpace(username)) return Results.BadRequest(new { Error = "Username cannot be empty." });
            ac.CreateOfflineAccount(username);
            var selected = ac.GetSelectedAccount();
            return Results.Ok(new { Message = "Offline account created and selected.", Username = selected?.Name, Identifier = selected?.UniqueId });
        });

        app.MapDelete("/api/accounts/{identifier}", async (string identifier, IAccountService ac) =>
        {
            var account = ac.Accounts.FirstOrDefault(a => a.UniqueId == identifier);
            if (account == null) return Results.NotFound(new { Error = $"Account '{identifier}' not found." });
            await ac.RemoveAccountAsync(account);
            return Results.Ok(new { Message = "Account removed successfully." });
        });

        // 2. Games/Instances Endpoints
        app.MapGet("/api/games", (Core c) =>
            c.Games.Select(g => new
            {
                g.Path.BasePath,
                g.Version.DisplayName,
                g.Version.BasedOn,
                Type = g.Version.Type,
                UsesCustomSettings = g.UsesCustomGameSettings,
                RunState = g.RunState.ToString()
            }));

        app.MapPost("/api/games", (CreateGameRequest req, Core c) =>
        {
            if (string.IsNullOrWhiteSpace(req.DisplayName) || string.IsNullOrWhiteSpace(req.BasedOn))
                return Results.BadRequest(new { Error = "DisplayName and BasedOn are required." });

            var version = new Emerald.CoreX.Versions.Version
            {
                DisplayName = req.DisplayName,
                BasedOn = req.BasedOn,
                ReleaseType = "release",
                ReleaseTime = DateTime.UtcNow
            };
            var game = c.CreateGame(version, req.FolderName);
            return Results.Ok(new
            {
                Message = "Instance created successfully.",
                BasePath = game.Path.BasePath,
                DisplayName = game.Version.DisplayName
            });
        });

        app.MapDelete("/api/games", (string basePath, bool deleteFolder, Core c) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == basePath);
            if (game == null) return Results.NotFound(new { Error = $"Game instance at path '{basePath}' not found." });
            c.RemoveGame(game, deleteFolder);
            return Results.Ok(new { Message = "Game instance removed successfully." });
        });

        app.MapGet("/api/versions/vanilla", (Core c) =>
            c.VanillaVersions.Select(v => new
            {
                v.DisplayName,
                v.BasedOn,
                v.Type,
                v.ReleaseTime
            }));

        // 3. Launch & Stop Endpoints
        app.MapPost("/api/games/launch", (string basePath, IGameRuntimeService runtime, Core c, IAccountService ac, ILogger<Program> logger) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == basePath);
            if (game == null) return Results.NotFound(new { Error = $"Game instance at path '{basePath}' not found." });
            
            var account = ac.GetSelectedAccount();
            if (account == null) return Results.BadRequest(new { Error = "No account selected. Please select or create an account first." });

            // Launch asynchronously in the background so we don't block the API
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("Launching game instance: {Name} ({Path})", game.Version.DisplayName, game.Path.BasePath);
                    await runtime.LaunchAsync(game, account);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to launch game instance {Name}", game.Version.DisplayName);
                }
            });

            return Results.Ok(new { Message = "Launch process initiated." });
        });

        app.MapPost("/api/games/stop", async (string basePath, string? mode, IGameRuntimeService runtime, Core c) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == basePath);
            if (game == null) return Results.NotFound(new { Error = $"Game instance at path '{basePath}' not found." });

            var stopMode = string.Equals(mode, "Force", StringComparison.OrdinalIgnoreCase) 
                ? GameStopMode.Force 
                : GameStopMode.Gentle;

            await runtime.StopAsync(game, stopMode);
            return Results.Ok(new { Message = $"Stop requested with mode: {stopMode}." });
        });

        app.MapGet("/api/games/sessions", (IGameRuntimeService runtime) =>
            runtime.Sessions.Select(s => new
            {
                s.GamePath,
                s.DisplayName,
                State = s.RunStateText,
                s.ProcessId,
                s.ExitCode,
                s.EndedAt,
                s.HasCrashReport
            }));

        // 4. Settings Endpoints
        app.MapGet("/api/settings/global", (IGlobalGameSettingsService gs) =>
            Results.Ok(gs.Settings));

        app.MapPut("/api/settings/global", async (HttpContext context, IGlobalGameSettingsService gs) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var newSettings = JsonSerializer.Deserialize<GameSettings>(body);
            if (newSettings == null) return Results.BadRequest(new { Error = "Invalid settings payload." });

            var current = gs.Settings;
            current.JavaPath = newSettings.JavaPath;
            current.MaximumRamMb = newSettings.MaximumRamMb;
            current.MinimumRamMb = newSettings.MinimumRamMb;
            current.ScreenWidth = newSettings.ScreenWidth;
            current.ScreenHeight = newSettings.ScreenHeight;
            current.FullScreen = newSettings.FullScreen;

            current.JVMArgs.Clear();
            foreach (var arg in newSettings.JVMArgs)
            {
                current.JVMArgs.Add(arg);
            }

            gs.Save();
            return Results.Ok(new { Message = "Global settings updated successfully.", Settings = current });
        });
    }
}

// --- ADAPTER / DUMMY MODELS FOR HEADLESS CONSOLE RUNTIME ---

public record CreateGameRequest(string DisplayName, string BasedOn, string? FolderName);

public class HeadlessGameRuntimeSettings : IGameRuntimeSettings
{
    public bool IsLogCaptureEnabled => true; // Enable log capture so we stream stdin/stdout logs over websockets
}
