using Emerald.CoreX.Models;

namespace Emerald.CoreX.Runtime;

public enum MinecraftLaunchTargetKind { Configured, MainMenu, Server, World }

public sealed record MinecraftLaunchTarget
{
    public MinecraftLaunchTargetKind Kind { get; init; } = MinecraftLaunchTargetKind.Configured;
    public string? ServerHost { get; init; }
    public int? ServerPort { get; init; }
    public string? WorldFolderName { get; init; }
    public string? DisplayName { get; init; }

    public static MinecraftLaunchTarget Configured { get; } = new();
    public static MinecraftLaunchTarget MainMenu { get; } = new() { Kind = MinecraftLaunchTargetKind.MainMenu };
    public static MinecraftLaunchTarget ForServer(string host, int port = 25565, string? displayName = null)
        => new() { Kind = MinecraftLaunchTargetKind.Server, ServerHost = host, ServerPort = port, DisplayName = displayName };
    public static MinecraftLaunchTarget ForWorld(string folderName, string? displayName = null)
        => new() { Kind = MinecraftLaunchTargetKind.World, WorldFolderName = folderName, DisplayName = displayName };
}

public sealed record GameLaunchRequest(Game Game, EAccount? Account = null, MinecraftLaunchTarget? Target = null, Guid? QuickProfileId = null)
{
    public MinecraftLaunchTarget EffectiveTarget => Target ?? MinecraftLaunchTarget.Configured;
}
