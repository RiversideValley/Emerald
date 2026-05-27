using Emerald.CoreX.Models;
using Emerald.CoreX.Store;

namespace Emerald.ApiHost;

public record CreateGameRequest(
    string DisplayName,
    string BasedOn,
    string? FolderName,
    string? LoaderType,
    string? ModVersion);

public record InitializeCoreRequest(string BasePath);

public record AccountSelectionRequest(string Identifier);

public record OfflineAccountRequest(string Username);

public record ElyByPasswordLoginRequest(string Login, string Password, string? TwoFactorCode);

public record GameInstallRequest(string BasePath, bool ShowFileProgress = false);

public record GameLaunchRequest(string BasePath);

public record GameStopRequest(string BasePath, string? Mode);

public record GameSettingsUpdateRequest(string BasePath, GameSettings Settings);

public record JavaRuntimeValidationRequest(string Path);

public record SharedStoreSettingsRequest(StoreLinkMode WindowsLinkMode, StoreLinkMode UnixLinkMode);
