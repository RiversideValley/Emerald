using Emerald.CoreX.Helpers;
using Emerald.CoreX.Runtime;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed class HomePreferences
{
    public int SchemaVersion { get; set; } = 1;
    public Guid? SelectedInstanceId { get; set; }
    public bool ShowAllEmeraldPlaytime { get; set; }
}

public interface IHomePreferencesService
{
    HomePreferences Load();
    void Save(HomePreferences preferences);
}

public sealed class HomePreferencesService(IMinecraftBaseSettingsService settings, ILogger<HomePreferencesService> logger) : IHomePreferencesService
{
    private bool _isReadOnly;
    public HomePreferences Load()
    {
        var value = settings.Get(SettingsKeys.HomePreferences, new HomePreferences());
        if (value.SchemaVersion > 1) { _isReadOnly = true; logger.LogWarning("Home preference schema {Schema} is newer than supported; Home preferences are read-only.", value.SchemaVersion); }
        return value;
    }
    public void Save(HomePreferences preferences) { if (!_isReadOnly) settings.Set(SettingsKeys.HomePreferences, preferences); }
}

public sealed class QuickProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string GlyphKey { get; set; } = "Play";
    public QuickProfileIconKind IconKind { get; set; } = QuickProfileIconKind.Glyph;
    public string? BlockIconFileName { get; set; }
    public uint AccentArgb { get; set; } = 0xFF107C10;
    public Guid InstanceId { get; set; }
    public string AccountUniqueId { get; set; } = string.Empty;
    public MinecraftLaunchTargetKind TargetKind { get; set; } = MinecraftLaunchTargetKind.MainMenu;
    public Guid? SavedServerId { get; set; }
    public string? WorldFolderName { get; set; }
    public string? TargetDisplayNameSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum QuickProfileIconKind
{
    Glyph,
    Block
}

public sealed record QuickProfileValidation(bool IsValid, IReadOnlyList<string> Messages);

public interface IQuickProfileService
{
    event EventHandler? Changed;
    IReadOnlyList<QuickProfile> GetAll();
    QuickProfile Save(QuickProfile profile);
    QuickProfile Duplicate(Guid id);
    bool Remove(Guid id);
    void Move(Guid id, int offset);
    QuickProfileValidation Validate(QuickProfile profile, IEnumerable<Game> games, IEnumerable<Models.EAccount> accounts, IEnumerable<Servers.SavedServer> servers);
}

internal sealed class QuickProfileEnvelope { public int SchemaVersion { get; set; } = 1; public List<QuickProfile> Profiles { get; set; } = []; }

public sealed class QuickProfileService(IMinecraftBaseSettingsService settings, ILogger<QuickProfileService> logger) : IQuickProfileService
{
    private readonly object _gate = new();
    private bool _isReadOnly;
    public event EventHandler? Changed;
    public IReadOnlyList<QuickProfile> GetAll() { lock (_gate) return Read().Profiles.ToArray(); }
    public QuickProfile Save(QuickProfile profile)
    {
        profile.Name = profile.Name.Trim(); profile.UpdatedAt = DateTimeOffset.UtcNow;
        lock (_gate) { var envelope = Read(); if (_isReadOnly) return profile; var index = envelope.Profiles.FindIndex(x => x.Id == profile.Id); if (index < 0) envelope.Profiles.Add(profile); else envelope.Profiles[index] = profile; Write(envelope); }
        Changed?.Invoke(this, EventArgs.Empty); return profile;
    }
    public QuickProfile Duplicate(Guid id)
    {
        var source = GetAll().First(x => x.Id == id);
        return Save(new QuickProfile { Name = source.Name + " copy", GlyphKey = source.GlyphKey, IconKind = source.IconKind, BlockIconFileName = source.BlockIconFileName, AccentArgb = source.AccentArgb, InstanceId = source.InstanceId, AccountUniqueId = source.AccountUniqueId, TargetKind = source.TargetKind, SavedServerId = source.SavedServerId, WorldFolderName = source.WorldFolderName, TargetDisplayNameSnapshot = source.TargetDisplayNameSnapshot });
    }
    public bool Remove(Guid id) { bool removed; lock (_gate) { var value = Read(); if (_isReadOnly) return false; removed = value.Profiles.RemoveAll(x => x.Id == id) > 0; if (removed) Write(value); } if (removed) Changed?.Invoke(this, EventArgs.Empty); return removed; }
    public void Move(Guid id, int offset) { lock (_gate) { var value = Read(); if (_isReadOnly) return; var from = value.Profiles.FindIndex(x => x.Id == id); if (from < 0) return; var to = Math.Clamp(from + offset, 0, value.Profiles.Count - 1); var item = value.Profiles[from]; value.Profiles.RemoveAt(from); value.Profiles.Insert(to, item); Write(value); } Changed?.Invoke(this, EventArgs.Empty); }
    public QuickProfileValidation Validate(QuickProfile profile, IEnumerable<Game> games, IEnumerable<Models.EAccount> accounts, IEnumerable<Servers.SavedServer> servers)
    {
        var messages = new List<string>();
        var game = games.FirstOrDefault(x => x.InstanceId == profile.InstanceId);
        if (game == null) messages.Add("The instance is no longer installed.");
        if (!accounts.Any(x => x.UniqueId == profile.AccountUniqueId)) messages.Add("The account is no longer available.");
        if (profile.TargetKind == MinecraftLaunchTargetKind.Server && (profile.SavedServerId == null || !servers.Any(x => x.Id == profile.SavedServerId))) messages.Add("Choose a saved server.");
        if (profile.TargetKind == MinecraftLaunchTargetKind.World && (game == null || string.IsNullOrWhiteSpace(profile.WorldFolderName) || !Directory.Exists(Path.Combine(game.Path.BasePath, "saves", profile.WorldFolderName)))) messages.Add("Choose an available world.");
        if (profile.IconKind == QuickProfileIconKind.Block && (string.IsNullOrWhiteSpace(profile.BlockIconFileName) || Path.GetFileName(profile.BlockIconFileName) != profile.BlockIconFileName)) messages.Add("Choose an icon.");
        if (string.IsNullOrWhiteSpace(profile.Name)) messages.Add("Enter a profile name.");
        return new(messages.Count == 0, messages);
    }
    private QuickProfileEnvelope Read() { var value = settings.Get(SettingsKeys.QuickProfiles, new QuickProfileEnvelope()); if (value.SchemaVersion > 1) { _isReadOnly = true; logger.LogWarning("Quick profile schema {Schema} is newer than supported; quick profiles are read-only.", value.SchemaVersion); } return value; }
    private void Write(QuickProfileEnvelope value) => settings.Set(SettingsKeys.QuickProfiles, value);
}
