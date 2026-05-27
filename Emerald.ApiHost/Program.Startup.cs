using CmlLib.Core;
using Emerald.ApiHost.Services;
using Emerald.CoreX;
using Emerald.CoreX.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Emerald.ApiHost;

public partial class Program
{
    private static void ConfigureApp(WebApplication app, string basePath)
    {
        app.UseWebSockets();
        MapEventSocket(app);
        StartCoreInitialization(app, basePath);
        MapApiRoutes(app.MapGroup("/api"));
    }

    private static void MapEventSocket(WebApplication app)
    {
        app.MapGet("/ws/events", async (HttpContext context, EventHub eventHub) =>
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
        })
        .WithName("EventsWebSocket")
        .WithTags("Events");
    }

    private static void StartCoreInitialization(WebApplication app, string basePath)
    {
        var core = app.Services.GetRequiredService<Core>();
        var accountService = app.Services.GetRequiredService<IAccountService>();

        _ = Task.Run(async () =>
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            try
            {
                logger.LogInformation("Initializing Emerald CoreX Headless engine...");

                const string MicrosoftClientId = "dfeccda7-604a-4895-b409-9d35f1679b5d";
                await accountService.InitializeAsync(MicrosoftClientId);

                var minecraftPath = new MinecraftPath(basePath);
                await core.InitializeAndRefresh(minecraftPath);

                logger.LogInformation("Emerald CoreX Headless engine initialized successfully! Minecraft base path: {Path}", basePath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Emerald CoreX Headless engine on startup.");
            }
        });
    }
}
