using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace Emerald.ApiHost;

public partial class Program
{
    public static void Main(string[] args)
    {
        var basePath = "";
        var port = 58136;
        string? socketPath = null;
        var swaggerEnabled = true;

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
            else if (args[i] == "--no-swagger" || args[i] == "--disable-swagger")
            {
                swaggerEnabled = false;
            }
            else if (args[i] == "--swagger" && i + 1 < args.Length && bool.TryParse(args[i + 1], out var parsedSwaggerEnabled))
            {
                swaggerEnabled = parsedSwaggerEnabled;
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
        ConfigureKestrel(builder, port, socketPath);
        ConfigureWebServices(builder.Services);
        ConfigureServices(builder.Services, basePath);

        var app = builder.Build();

        Ioc.Default.ConfigureServices(app.Services);

        ConfigureApp(app, basePath);
        ConfigureSwagger(app, swaggerEnabled);
        ConfigureSocketCleanup(app, socketPath);

        app.Run();
    }

    private static void ConfigureKestrel(WebApplicationBuilder builder, int port, string? socketPath)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (!string.IsNullOrEmpty(socketPath))
            {
                var fullSocketPath = Path.GetFullPath(socketPath);
                var socketDirectory = Path.GetDirectoryName(fullSocketPath);
                if (!string.IsNullOrWhiteSpace(socketDirectory))
                {
                    Directory.CreateDirectory(socketDirectory);
                }

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
    }

    private static void ConfigureWebServices(IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Emerald Headless API",
                Version = "v1",
                Description = "Localhost and Unix socket API for driving Emerald CoreX from native frontends."
            });
        });
    }

    private static void ConfigureSwagger(WebApplication app, bool swaggerEnabled)
    {
        if (!swaggerEnabled)
        {
            return;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Emerald Headless API v1");
            options.RoutePrefix = "swagger";
        });
    }

    private static void ConfigureSocketCleanup(WebApplication app, string? socketPath)
    {
        if (string.IsNullOrEmpty(socketPath))
        {
            return;
        }

        var fullSocketPath = Path.GetFullPath(socketPath);
        app.Lifetime.ApplicationStopped.Register(() =>
        {
            try
            {
                if (File.Exists(fullSocketPath))
                {
                    File.Delete(fullSocketPath);
                }
            }
            catch
            {
                // Best-effort cleanup for stale Unix socket files.
            }
        });
    }
}
