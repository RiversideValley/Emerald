using CmlLib.Core;
using Emerald.CoreX;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Emerald.ApiHost;

public partial class Program
{
    private static CancellationTokenSource? _loginCts;
    private static Task<bool>? _loginTask;

    private static void MapApiRoutes(RouteGroupBuilder api)
    {
        MapStatusRoutes(api);
        MapAccountRoutes(api);
        MapGameRoutes(api);
        MapVersionRoutes(api);
        MapRuntimeRoutes(api);
        MapSettingsRoutes(api);
        MapJavaRoutes(api);
        MapNotificationRoutes(api);
    }

    private static void MapStatusRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/status", (Core c) => Results.Ok(new
        {
            Initialized = c.Initialized,
            IsRefreshing = c.IsRefreshing,
            IsOfflineMode = c.IsOfflineMode,
            BasePath = c.BasePath?.BasePath,
            GamesCount = c.Games.Count,
            VanillaVersionsCount = c.VanillaVersions.Count
        }))
        .WithName("GetStatus")
        .WithTags("Status");

        api.MapPost("/core/initialize", async (InitializeCoreRequest req, Core c, ILogger<Program> logger) =>
        {
            if (string.IsNullOrWhiteSpace(req.BasePath))
            {
                return Results.BadRequest(new { Error = "BasePath is required." });
            }

            var minecraftPath = Path.GetFullPath(req.BasePath);
            Directory.CreateDirectory(minecraftPath);

            try
            {
                await c.InitializeAndRefresh(new MinecraftPath(minecraftPath));
                return Results.Ok(new
                {
                    Message = "Core initialized.",
                    BasePath = minecraftPath,
                    c.Initialized,
                    c.IsOfflineMode,
                    GamesCount = c.Games.Count,
                    VanillaVersionsCount = c.VanillaVersions.Count
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize CoreX for {BasePath}", minecraftPath);
                return Results.Problem(ex.Message);
            }
        })
        .WithName("InitializeCore")
        .WithTags("Status");
    }

    private static void MapAccountRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/accounts", (IAccountService ac) =>
            ac.Accounts.Select(a => new
            {
                Identifier = a.UniqueId,
                Username = a.Name,
                Type = a.Type.ToString(),
                LastAccess = a.LastUsed
            }))
            .WithName("ListAccounts")
            .WithTags("Accounts");

        api.MapGet("/accounts/selected", (IAccountService ac) =>
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
        })
        .WithName("GetSelectedAccount")
        .WithTags("Accounts");

        api.MapPost("/accounts/select", (AccountSelectionRequest req, IAccountService ac) =>
        {
            var account = ac.Accounts.FirstOrDefault(a => a.UniqueId == req.Identifier);
            if (account == null) return Results.NotFound(new { Error = $"Account with identifier '{req.Identifier}' not found." });
            ac.SetSelectedAccount(account);
            return Results.Ok(new { Message = "Account selected successfully.", Username = account.Name });
        })
        .WithName("SelectAccount")
        .WithTags("Accounts");

        api.MapPost("/accounts/offline", (OfflineAccountRequest req, IAccountService ac) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username)) return Results.BadRequest(new { Error = "Username cannot be empty." });
            ac.CreateOfflineAccount(req.Username);
            var selected = ac.GetSelectedAccount();
            return Results.Ok(new { Message = "Offline account created and selected.", Username = selected?.Name, Identifier = selected?.UniqueId });
        })
        .WithName("CreateOfflineAccount")
        .WithTags("Accounts");

        api.MapDelete("/accounts/{identifier}", async (string identifier, IAccountService ac) =>
        {
            var account = ac.Accounts.FirstOrDefault(a => a.UniqueId == identifier);
            if (account == null) return Results.NotFound(new { Error = $"Account '{identifier}' not found." });
            await ac.RemoveAccountAsync(account);
            return Results.Ok(new { Message = "Account removed successfully." });
        })
        .WithName("RemoveAccount")
        .WithTags("Accounts");

        api.MapPost("/accounts/login/microsoft/start", (IAccountService ac) =>
        {
            _loginCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            _loginTask = Task.Run(async () =>
            {
                await ac.SignInMicrosoftAccountAsync(_loginCts.Token);
                return true;
            }, _loginCts.Token);

            return Results.Accepted();
        })
        .WithName("StartMicrosoftLogin")
        .WithTags("Accounts");

        api.MapGet("/accounts/login/microsoft/status", async (IAccountService ac) =>
        {
            if (_loginTask == null) return Results.NotFound(new { Status = "no_login_in_progress" });
            if (!_loginTask.IsCompleted) return Results.Ok(new { Status = "pending" });
            if (_loginTask.IsFaulted) return Results.BadRequest(new { Status = "failed", Error = _loginTask.Exception?.InnerException?.Message });

            var selected = ac.GetSelectedAccount();
            return Results.Ok(new { Status = "completed", Username = selected?.Name, Identifier = selected?.UniqueId });
        })
        .WithName("GetMicrosoftLoginStatus")
        .WithTags("Accounts");

        api.MapPost("/accounts/login/microsoft/cancel", () =>
        {
            _loginCts?.Cancel();
            return Results.Ok(new { Message = "Login cancelled." });
        })
        .WithName("CancelMicrosoftLogin")
        .WithTags("Accounts");

        api.MapPost("/accounts/login/elyby/browser", async (IAccountService ac, ILogger<Program> logger) =>
        {
            try
            {
                await ac.SignInElyByAccountAsync();
                var selected = ac.GetSelectedAccount();
                return Results.Ok(new { Message = "Ely.by sign-in completed.", Username = selected?.Name });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ely.by browser sign-in failed");
                return Results.Problem(ex.Message);
            }
        })
        .WithName("StartElyByBrowserLogin")
        .WithTags("Accounts");

        api.MapPost("/accounts/login/elyby/password", async (
            ElyByPasswordLoginRequest req,
            IAccountService ac, ILogger<Program> logger) =>
        {
            try
            {
                await ac.SignInElyByAccountAsync(req.Login, req.Password, req.TwoFactorCode);
                var selected = ac.GetSelectedAccount();
                return Results.Ok(new { Message = "Ely.by sign-in completed.", Username = selected?.Name });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ely.by password sign-in failed");
                return Results.Problem(ex.Message);
            }
        })
        .WithName("SignInElyByWithPassword")
        .WithTags("Accounts");
    }

    private static void MapGameRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/games", (Core c) =>
        {
            if (!c.Initialized)
                return Results.StatusCode(503);

            return Results.Ok(c.Games.Select(g => new
            {
                g.Path.BasePath,
                g.Version.DisplayName,
                g.Version.BasedOn,
                Type = g.Version.Type,
                UsesCustomSettings = g.UsesCustomGameSettings,
                RunState = g.RunState.ToString()
            }));
        })
        .WithName("ListGames")
        .WithTags("Games");

        api.MapPost("/games", (CreateGameRequest req, Core c) =>
        {
            if (!c.Initialized)
                return Results.StatusCode(503);

            if (string.IsNullOrWhiteSpace(req.DisplayName) || string.IsNullOrWhiteSpace(req.BasedOn))
                return Results.BadRequest(new { Error = "DisplayName and BasedOn are required." });

            var loaderType = Enum.TryParse<Emerald.CoreX.Versions.Type>(req.LoaderType, ignoreCase: true, out var parsed)
                ? parsed
                : Emerald.CoreX.Versions.Type.Vanilla;

            var version = new Emerald.CoreX.Versions.Version
            {
                DisplayName = req.DisplayName,
                BasedOn = req.BasedOn,
                Type = loaderType,
                ModVersion = req.ModVersion,
                ReleaseType = "release",
                ReleaseTime = DateTime.UtcNow
            };

            var game = c.CreateGame(version, req.FolderName);
            return Results.Ok(new
            {
                Message = "Instance created.",
                BasePath = game.Path.BasePath,
                DisplayName = game.Version.DisplayName,
                LoaderType = game.Version.Type.ToString()
            });
        })
        .WithName("CreateGame")
        .WithTags("Games");

        api.MapDelete("/games", (string basePath, bool deleteFolder, Core c) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == basePath);
            if (game == null) return Results.NotFound(new { Error = $"Game instance at path '{basePath}' not found." });
            c.RemoveGame(game, deleteFolder);
            return Results.Ok(new { Message = "Game instance removed successfully." });
        })
        .WithName("RemoveGame")
        .WithTags("Games");

        api.MapPost("/games/install", async (GameInstallRequest req, Core c, ILogger<Program> logger) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == req.BasePath);
            if (game == null)
                return Results.NotFound(new { Error = $"Game instance '{req.BasePath}' not found." });

            try
            {
                await c.InstallGameOrThrow(game, req.ShowFileProgress);
                return Results.Ok(new { Message = $"Installed {game.Version.DisplayName} successfully." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Install failed for {Name}", game.Version.DisplayName);
                return Results.Problem(ex.Message);
            }
        })
        .WithName("InstallGame")
        .WithTags("Games");

        api.MapPost("/games/launch", (GameLaunchRequest req, IGameRuntimeService runtime, Core c, IAccountService ac, ILogger<Program> logger) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == req.BasePath);
            if (game == null) return Results.NotFound(new { Error = $"Game instance at path '{req.BasePath}' not found." });

            if (string.IsNullOrWhiteSpace(game.Version.RealVersion))
                return Results.BadRequest(new
                {
                    Error = $"{game.Version.DisplayName} has not been installed yet. Call /api/games/install first."
                });

            var account = ac.GetSelectedAccount();
            if (account == null) return Results.BadRequest(new { Error = "No account selected. Please select or create an account first." });

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
        })
        .WithName("LaunchGame")
        .WithTags("Games");

        api.MapPost("/games/stop", async (GameStopRequest req, IGameRuntimeService runtime, Core c) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == req.BasePath);
            if (game == null) return Results.NotFound(new { Error = $"Game instance at path '{req.BasePath}' not found." });

            var stopMode = string.Equals(req.Mode, "Force", StringComparison.OrdinalIgnoreCase)
                ? GameStopMode.Force
                : GameStopMode.Gentle;

            await runtime.StopAsync(game, stopMode);
            return Results.Ok(new { Message = $"Stop requested with mode: {stopMode}." });
        })
        .WithName("StopGame")
        .WithTags("Games");
    }

    private static void MapVersionRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/versions/vanilla", (Core c) =>
        {
            if (!c.Initialized)
                return Results.StatusCode(503);

            return Results.Ok(c.VanillaVersions.Select(v => new
            {
                v.DisplayName,
                v.BasedOn,
                v.Type,
                v.ReleaseTime
            }));
        })
        .WithName("ListVanillaVersions")
        .WithTags("Versions");

        api.MapGet("/versions/loaders", async (
            string mcVersion, string loaderType,
            ModLoaderRouter router) =>
        {
            if (!Enum.TryParse<Emerald.CoreX.Versions.Type>(loaderType, ignoreCase: true, out var type)
                || type == Emerald.CoreX.Versions.Type.Vanilla)
                return Results.BadRequest(new { Error = "Provide a valid mod loader type." });

            var installer = router.Installers.FirstOrDefault(i => i.Type == type);
            if (installer == null)
                return Results.BadRequest(new { Error = $"No installer registered for '{loaderType}'." });

            var versions = await installer.GetVersionsAsync(mcVersion);
            return Results.Ok(versions.Select(v => new { v.Version, v.Stable, v.Tag }));
        })
        .WithName("ListLoaderVersions")
        .WithTags("Versions");
    }

    private static void MapRuntimeRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/games/sessions", (IGameRuntimeService runtime) =>
            runtime.Sessions.Select(s => new
            {
                s.GamePath,
                s.DisplayName,
                State = s.RunStateText,
                s.ProcessId,
                s.ExitCode,
                s.EndedAt,
                s.HasCrashReport
            }))
            .WithName("ListGameSessions")
            .WithTags("Runtime");

        api.MapGet("/games/logs", (
            string basePath, int? page, int? pageSize, string? level,
            IGameRuntimeService runtime) => GetSessionLogs(basePath, page, pageSize, level, runtime))
            .WithName("GetGameLogs")
            .WithTags("Runtime");

        api.MapGet("/games/sessions/{gamePath}/logs", (
            string gamePath, int? page, int? pageSize, string? level,
            IGameRuntimeService runtime) => GetSessionLogs(gamePath, page, pageSize, level, runtime))
            .WithName("GetGameSessionLogs")
            .WithTags("Runtime");
    }

    private static void MapSettingsRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/settings/global", (IGlobalGameSettingsService gs) =>
            Results.Ok(gs.Settings))
            .WithName("GetGlobalGameSettings")
            .WithTags("Settings");

        api.MapPut("/settings/global", (GameSettings newSettings, IGlobalGameSettingsService gs) =>
        {
            if (newSettings == null) return Results.BadRequest(new { Error = "Invalid settings payload." });

            var current = gs.Settings;
            current.ApplyFrom(newSettings);
            gs.Save();
            return Results.Ok(new { Message = "Global settings updated successfully.", Settings = current });
        })
        .WithName("UpdateGlobalGameSettings")
        .WithTags("Settings");

        api.MapGet("/settings/shared-store", (IStoreSharedContentSettingsService settingsService) =>
            Results.Ok(settingsService.Settings))
            .WithName("GetSharedStoreSettings")
            .WithTags("Settings");

        api.MapPut("/settings/shared-store", (SharedStoreSettingsRequest req, IStoreSharedContentSettingsService settingsService) =>
        {
            settingsService.Settings.WindowsLinkMode = req.WindowsLinkMode;
            settingsService.Settings.UnixLinkMode = req.UnixLinkMode;
            settingsService.Save();
            return Results.Ok(new { Message = "Shared store settings updated successfully.", Settings = settingsService.Settings });
        })
        .WithName("UpdateSharedStoreSettings")
        .WithTags("Settings");

        api.MapGet("/games/settings", (string basePath, Core c) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == basePath);
            if (game == null) return Results.NotFound();
            return Results.Ok(new
            {
                UsesCustomSettings = game.UsesCustomGameSettings,
                Settings = game.EffectiveSettings
            });
        })
        .WithName("GetGameSettings")
        .WithTags("Settings");

        api.MapPut("/games/settings", (GameSettingsUpdateRequest req, Core c) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == req.BasePath);
            if (game == null) return Results.NotFound();

            var incoming = req.Settings;
            if (incoming == null) return Results.BadRequest();

            game.UsesCustomGameSettings = true;
            game.GetEditableSettings().ApplyFrom(incoming);
            c.SaveGames();
            return Results.Ok(new { Message = "Game settings updated." });
        })
        .WithName("UpdateGameSettings")
        .WithTags("Settings");

        api.MapDelete("/games/settings", (string basePath, Core c) =>
        {
            var game = c.Games.FirstOrDefault(g => g.Path.BasePath == basePath);
            if (game == null) return Results.NotFound();
            game.UsesCustomGameSettings = false;
            c.SaveGames();
            return Results.Ok(new { Message = "Game reset to global settings." });
        })
        .WithName("ResetGameSettings")
        .WithTags("Settings");
    }

    private static void MapJavaRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/java/runtimes", async (
            string? minecraftRootPath,
            string[]? savedPaths,
            IJavaRuntimeCatalogService javaCatalog,
            Core c,
            CancellationToken cancellationToken) =>
        {
            var rootPath = minecraftRootPath ?? c.BasePath?.BasePath;
            var runtimes = await javaCatalog.DiscoverAsync(rootPath, savedPaths, cancellationToken);
            return Results.Ok(runtimes);
        })
        .WithName("ListJavaRuntimes")
        .WithTags("Java");

        api.MapPost("/java/validate", async (
            JavaRuntimeValidationRequest req,
            IJavaRuntimeCatalogService javaCatalog,
            CancellationToken cancellationToken) =>
        {
            var result = await javaCatalog.ValidateAsync(req.Path, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("ValidateJavaRuntime")
        .WithTags("Java");
    }

    private static void MapNotificationRoutes(RouteGroupBuilder api)
    {
        api.MapGet("/notifications", (INotificationService ns) =>
            ns.ActiveNotifications.Select(n => new
            {
                n.Id, n.Title, n.Message,
                Type = n.Type.ToString(),
                n.Progress, n.IsIndeterminate,
                n.IsCompleted, n.IsCancellable,
                n.Timestamp
            }))
            .WithName("ListNotifications")
            .WithTags("Notifications");

        api.MapPost("/notifications/{id}/cancel", (string id, INotificationService ns) =>
        {
            ns.Cancel(id);
            return Results.Ok();
        })
        .WithName("CancelNotification")
        .WithTags("Notifications");

        api.MapDelete("/notifications/{id}", (string id, INotificationService ns) =>
        {
            ns.RemoveNotification(id);
            return Results.Ok();
        })
        .WithName("RemoveNotification")
        .WithTags("Notifications");
    }
}
