using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.GameOptions;

/// <summary>Edits only catalogued Minecraft settings and preserves every other line.</summary>
public sealed class MinecraftOptionsService : IMinecraftOptionsService
{
    private readonly ILogger<MinecraftOptionsService> _logger;

    public MinecraftOptionsService(ILogger<MinecraftOptionsService> logger) => _logger = logger;

    public async Task<MinecraftOptionsLoadResult> LoadAsync(Game game, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(game.Path.BasePath, "options.txt");
        if (!File.Exists(path)) return new MinecraftOptionsLoadResult { OptionsFileExists = false };

        var document = await MinecraftOptionsDocument.ReadAsync(path, cancellationToken);
        var lang = await LoadLanguageAsync(game, cancellationToken);
        var entries = document.EffectiveOptions
            .Select(pair => MinecraftOptionCatalog.Create(pair.Key, pair.Value, lang))
            .Where(entry => entry.Type != MinecraftOptionType.Skip)
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation("Loaded {Count} options for {Name}.", entries.Count, game.Version.DisplayName);
        return new MinecraftOptionsLoadResult { Entries = entries, OptionsFileExists = true };
    }

    public async Task<MinecraftOptionsSaveResult> SaveAsync(Game game, IEnumerable<MinecraftOptionEntry> entries, CancellationToken cancellationToken = default)
    {
        var dirty = entries.Where(entry => entry.IsEditable && entry.IsDirty).ToArray();
        if (dirty.Length == 0) return new MinecraftOptionsSaveResult { Status = MinecraftOptionsSaveStatus.NoChanges };

        var path = Path.Combine(game.Path.BasePath, "options.txt");
        if (!File.Exists(path))
            return new MinecraftOptionsSaveResult { Status = MinecraftOptionsSaveStatus.Conflict, ConflictingKeys = dirty.Select(x => x.Key).ToArray() };

        var document = await MinecraftOptionsDocument.ReadAsync(path, cancellationToken);
        var conflicts = dirty
            .Where(entry => !document.TryGetEffectiveValue(entry.Key, out var current) || current != entry.OriginalRawValue)
            .Select(entry => entry.Key)
            .ToArray();
        if (conflicts.Length > 0)
            return new MinecraftOptionsSaveResult { Status = MinecraftOptionsSaveStatus.Conflict, ConflictingKeys = conflicts };

        await document.PatchLastOccurrences(dirty.ToDictionary(entry => entry.Key, entry => entry.RawValue, StringComparer.Ordinal))
            .WriteReplacingAsync(path, cancellationToken);
        foreach (var entry in dirty) entry.AcceptChanges();
        _logger.LogInformation("Saved {Count} options for {Name}.", dirty.Length, game.Version.DisplayName);
        return new MinecraftOptionsSaveResult { Status = MinecraftOptionsSaveStatus.Saved };
    }

    private async Task<Dictionary<string, string>> LoadLanguageAsync(Game game, CancellationToken ct)
    {
        var english = await LoadAssetLanguageAsync(game, "en_us", ct);
        if (english.Count == 0) english = await LoadJarLanguageAsync(game, "en_us", ct);
        foreach (var locale in GetLanguageCandidates().Where(locale => locale != "en_us"))
        {
            var localized = await LoadAssetLanguageAsync(game, locale, ct);
            if (localized.Count == 0) continue;
            foreach (var pair in localized) english[pair.Key] = pair.Value;
            break;
        }
        return english;
    }

    private static IEnumerable<string> GetLanguageCandidates()
    {
        var culture = CultureInfo.CurrentUICulture;
        var exact = culture.Name.Replace('-', '_').ToLowerInvariant();
        if (!string.IsNullOrEmpty(exact)) yield return exact;
        if (!string.IsNullOrEmpty(culture.Name))
        {
            var specific = CultureInfo.CreateSpecificCulture(culture.Name).Name.Replace('-', '_').ToLowerInvariant();
            if (!string.Equals(exact, specific, StringComparison.Ordinal)) yield return specific;
        }
        yield return "en_us";
    }

    private async Task<Dictionary<string, string>> LoadAssetLanguageAsync(Game game, string locale, CancellationToken ct)
    {
        try
        {
            var baseVersion = game.Version.BasedOn;
            var versionPath = Path.Combine(game.Path.Versions, baseVersion, baseVersion + ".json");
            if (!File.Exists(versionPath)) return [];
            using var version = JsonDocument.Parse(await File.ReadAllBytesAsync(versionPath, ct));
            if (!version.RootElement.TryGetProperty("assetIndex", out var index) || !index.TryGetProperty("id", out var id)) return [];
            var indexPath = Path.Combine(game.Path.Assets, "indexes", id.GetString() + ".json");
            if (!File.Exists(indexPath)) return [];
            using var assetIndex = JsonDocument.Parse(await File.ReadAllBytesAsync(indexPath, ct));
            if (!assetIndex.RootElement.TryGetProperty("objects", out var objects)) return [];
            if (!objects.TryGetProperty("minecraft/lang/" + locale + ".json", out var asset) || !asset.TryGetProperty("hash", out var hashElement)) return [];
            var hash = hashElement.GetString();
            if (string.IsNullOrEmpty(hash)) return [];
            var path = Path.Combine(game.Path.Assets, "objects", hash[..2], hash);
            if (!File.Exists(path)) return [];
            await using var stream = File.OpenRead(path);
            return await ParseJsonLanguageAsync(stream, ct);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Unable to load localized Minecraft language assets."); return []; }
    }

    private async Task<Dictionary<string, string>> LoadJarLanguageAsync(Game game, string locale, CancellationToken ct)
    {
        try
        {
            var baseVersion = game.Version.BasedOn;
            var jarPath = Path.Combine(game.Path.Versions, baseVersion, baseVersion + ".jar");
            if (!File.Exists(jarPath)) return [];
            using var jar = ZipFile.OpenRead(jarPath);
            var json = jar.GetEntry("assets/minecraft/lang/" + locale + ".json");
            if (json is not null) { await using var stream = json.Open(); return await ParseJsonLanguageAsync(stream, ct); }
            var legacy = jar.GetEntry("assets/minecraft/lang/en_US.lang");
            if (legacy is null) return [];
            await using var legacyStream = legacy.Open();
            using var reader = new StreamReader(legacyStream);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                var index = line.IndexOf('=');
                if (index > 0) result[line[..index].Trim()] = line[(index + 1)..].Trim();
            }
            return result;
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Unable to load Minecraft language from version JAR."); return []; }
    }

    private static async Task<Dictionary<string, string>> ParseJsonLanguageAsync(Stream stream, CancellationToken ct)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed class MinecraftOptionsDocument
{
    private readonly IReadOnlyList<Line> _lines;
    private readonly bool _hasBom;
    private MinecraftOptionsDocument(IReadOnlyList<Line> lines, bool hasBom) { _lines = lines; _hasBom = hasBom; }

    public IEnumerable<KeyValuePair<string, string>> EffectiveOptions
    {
        get
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in _lines) if (line.Key is not null) map[line.Key] = line.Value!;
            return map;
        }
    }

    public static async Task<MinecraftOptionsDocument> ReadAsync(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = new UTF8Encoding(false, true).GetString(hasBom ? bytes.AsSpan(3) : bytes);
        var lines = new List<Line>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not '\r' and not '\n') continue;
            var length = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
            lines.Add(Line.Parse(text[start..index], text.Substring(index, length)));
            index += length - 1;
            start = index + 1;
        }
        if (start < text.Length) lines.Add(Line.Parse(text[start..], string.Empty));
        return new MinecraftOptionsDocument(lines, hasBom);
    }

    public bool TryGetEffectiveValue(string key, out string value)
    {
        for (var index = _lines.Count - 1; index >= 0; index--)
            if (string.Equals(_lines[index].Key, key, StringComparison.Ordinal)) { value = _lines[index].Value!; return true; }
        value = string.Empty;
        return false;
    }

    public MinecraftOptionsDocument PatchLastOccurrences(IReadOnlyDictionary<string, string> values)
    {
        var remaining = new Dictionary<string, string>(values, StringComparer.Ordinal);
        var output = _lines.ToArray();
        for (var index = output.Length - 1; index >= 0 && remaining.Count > 0; index--)
            if (output[index].Key is { } key && remaining.Remove(key, out var value)) output[index] = output[index].WithValue(value);
        return new MinecraftOptionsDocument(output, _hasBom);
    }

    public async Task WriteReplacingAsync(string path, CancellationToken ct)
    {
        var text = string.Concat(_lines.Select(line => line.Body + line.Newline));
        var content = new UTF8Encoding(false).GetBytes(text);
        var payload = _hasBom
            ? new UTF8Encoding(true).GetPreamble().Concat(content).ToArray()
            : content;
        var temporary = path + ".emerald-options-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(payload, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            File.Replace(temporary, path, null);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed class Line
    {
        public Line(string body, string newline, string? key, string? value, int valueStart)
        {
            Body = body;
            Newline = newline;
            Key = key;
            Value = value;
            ValueStart = valueStart;
        }

        public string Body { get; }
        public string Newline { get; }
        public string? Key { get; }
        public string? Value { get; }
        public int ValueStart { get; }

        public static Line Parse(string body, string newline)
        {
            var colon = body.IndexOf(':');
            if (colon < 1) return new Line(body, newline, null, null, 0);
            var key = body[..colon].Trim();
            if (key.Length == 0) return new Line(body, newline, null, null, 0);
            var start = colon + 1;
            while (start < body.Length && (body[start] == ' ' || body[start] == '\t')) start++;
            return new Line(body, newline, key, body[start..], start);
        }
        public Line WithValue(string value) => new(Body[..ValueStart] + value, Newline, Key, value, ValueStart);
    }
}

internal static class MinecraftOptionCatalog
{
    private static readonly HashSet<string> Hidden = new(StringComparer.Ordinal) { "version", "lang", "lastServer", "resourcePacks", "incompatibleResourcePacks", "tutorialStep", "startedCleanly", "joinedFirstServer" };
    private static readonly HashSet<string> Booleans = new(StringComparer.Ordinal) { "ao", "enableVsync", "entityShadows", "fullscreen", "bobView", "forceUnicodeFont", "highContrast", "showSubtitles", "directionalAudio", "autoJump", "discrete_mouse_scroll", "invertYMouse", "invertXMouse", "touchscreen", "toggleCrouch", "toggleSprint", "toggleAttack", "toggleUse", "chatColors", "chatLinks", "chatLinksPrompt", "backgroundForChatOnly", "hideLightningFlashes", "hideSplashTexts", "darkMojangStudiosBackground", "rawMouseInput", "narratorHotkey", "hideServerAddress", "advancedItemTooltips", "pauseOnLostFocus", "modelPart_cape", "modelPart_jacket", "modelPart_left_sleeve", "modelPart_right_sleeve", "modelPart_left_pants_leg", "modelPart_right_pants_leg", "modelPart_hat" };
    private static readonly Dictionary<string, (double Min, double Max, bool Integer, double Step, string? Suffix, MinecraftOptionCategory Category, double Multiplier, double Offset)> Sliders = new(StringComparer.Ordinal)
    {
        ["fov"] = (30,110,true,1,"°",MinecraftOptionCategory.Graphics,40,70), ["renderDistance"] = (2,32,true,1," chunks",MinecraftOptionCategory.Graphics,1,0), ["simulationDistance"] = (5,32,true,1," chunks",MinecraftOptionCategory.Graphics,1,0), ["maxFps"] = (10,260,true,10," fps",MinecraftOptionCategory.Graphics,1,0), ["mipmapLevels"] = (0,4,true,1,null,MinecraftOptionCategory.Graphics,1,0), ["biomeBlendRadius"] = (0,7,true,1,null,MinecraftOptionCategory.Graphics,1,0), ["menuBackgroundBlurriness"] = (0,10,true,1,null,MinecraftOptionCategory.Accessibility,1,0), ["entityDistanceScaling"] = (.5,5,false,.1,"×",MinecraftOptionCategory.Graphics,1,0), ["notificationDisplayTime"] = (.5,10,false,.5,"s",MinecraftOptionCategory.Accessibility,1,0), ["sprintWindow"] = (0,10,true,1,null,MinecraftOptionCategory.Controls,1,0), ["weatherRadius"] = (3,10,true,1,null,MinecraftOptionCategory.Graphics,1,0), ["cloudRange"] = (2,128,true,1,null,MinecraftOptionCategory.Graphics,1,0)
    };
    private static readonly HashSet<string> UnitSliders = new(StringComparer.Ordinal) { "gamma", "mouseSensitivity", "fovEffectScale", "screenEffectScale", "darknessEffectScale", "damageTiltStrength", "glintSpeed", "glintStrength", "chatOpacity", "chatLineSpacing", "chatDelay", "chatWidth", "chatHeightFocused", "chatHeightUnfocused", "chatScale", "textBackgroundOpacity", "mouseWheelSensitivity", "panoramaScrollSpeed" };

    public static MinecraftOptionEntry Create(string key, string raw, IReadOnlyDictionary<string, string> lang)
    {
        if (Hidden.Contains(key)) return Entry(key, raw, Prettify(key), MinecraftOptionCategory.General, MinecraftOptionType.Skip);
        if (key.StartsWith("key_", StringComparison.Ordinal)) return Entry(key, raw, ResolveKeyBindName(key, raw, lang), MinecraftOptionCategory.KeyBindings, MinecraftOptionType.KeyBind);
        if (key.StartsWith("soundCategory_", StringComparison.Ordinal) && IsNumber(raw)) return Slider(key, raw, Resolve(lang, "soundCategory." + key["soundCategory_".Length..], Prettify(key)), MinecraftOptionCategory.Sound, 0, 1, false, .01, null, 1, 0, MinecraftOptionType.SoundVolume);
        if (Booleans.Contains(key) && raw is "true" or "false") return Entry(key, raw, Resolve(lang, "options." + key, Prettify(key)), CategoryFor(key), MinecraftOptionType.Boolean);
        if (Sliders.TryGetValue(key, out var slider) && IsNumber(raw)) return Slider(key, raw, Resolve(lang, "options." + key, Prettify(key)), slider.Category, slider.Min, slider.Max, slider.Integer, slider.Step, slider.Suffix, slider.Multiplier, slider.Offset, slider.Integer ? MinecraftOptionType.IntSlider : MinecraftOptionType.FloatSlider);
        if (UnitSliders.Contains(key) && IsNumber(raw)) return Slider(key, raw, Resolve(lang, "options." + key, Prettify(key)), CategoryFor(key), 0, 1, false, .01, null, 1, 0, MinecraftOptionType.FloatSlider);
        if (TryNumericEnum(key, raw, lang, out var numeric)) return numeric;
        if (TryStringEnum(key, raw, lang, out var text)) return text;
        return Entry(key, raw, Resolve(lang, "options." + key, Prettify(key)), MinecraftOptionCategory.Unsupported, MinecraftOptionType.ReadOnly);
    }

    private static bool TryNumericEnum(string key, string raw, IReadOnlyDictionary<string, string> lang, out MinecraftOptionEntry entry)
    {
        string[]? values = key switch { "graphicsMode" => ["0","1","2"], "particles" => ["0","1","2"], "narrator" => ["0","1","2","3"], "chatVisibility" => ["0","1","2"], "attackIndicator" => ["0","1","2"], "prioritizeChunkUpdates" => ["0","1","2"], "difficulty" => ["0","1","2","3"], "ao" when raw is not "true" and not "false" => ["0","1","2"], _ => null };
        if (values is null || !values.Contains(raw, StringComparer.Ordinal)) { entry = null!; return false; }
        entry = Enum(key, raw, Resolve(lang, "options." + key, Prettify(key)), CategoryFor(key), values.Select(value => new MinecraftEnumOption(value, ResolveEnumLabel(key, value, lang))));
        return true;
    }

    private static bool TryStringEnum(string key, string raw, IReadOnlyDictionary<string, string> lang, out MinecraftOptionEntry entry)
    {
        var bare = raw.Trim('"');
        string[]? values = key switch { "mainHand" => ["left","right"], "renderClouds" => ["false","fast","true"], "graphicsPreset" => ["fast","fancy","fabulous","custom"], "inactivityFpsLimit" => ["minimized","afk"], _ => null };
        if (values is null || !values.Contains(bare, StringComparer.Ordinal)) { entry = null!; return false; }
        var quoted = raw.StartsWith('"');
        entry = Enum(key, raw, Resolve(lang, "options." + key, Prettify(key)), CategoryFor(key), values.Select(value => new MinecraftEnumOption(quoted ? "\"" + value + "\"" : value, ResolveEnumLabel(key, value, lang))));
        return true;
    }

    private static MinecraftOptionEntry Entry(string key, string raw, string name, MinecraftOptionCategory category, MinecraftOptionType type) { var entry = new MinecraftOptionEntry { Key = key, DisplayName = name, Category = category, Type = type, RawValue = raw }; entry.AcceptChanges(); return entry; }
    private static MinecraftOptionEntry Slider(string key, string raw, string name, MinecraftOptionCategory category, double min, double max, bool integer, double step, string? suffix, double multiplier, double offset, MinecraftOptionType type) { var entry = new MinecraftOptionEntry { Key = key, DisplayName = name, Category = category, Type = type, RawValue = raw, SliderMin = min, SliderMax = max, SliderIsInt = integer, SliderStep = step, SliderSuffix = suffix, SliderStorageMultiplier = multiplier, SliderStorageOffset = offset }; entry.AcceptChanges(); return entry; }
    private static MinecraftOptionEntry Enum(string key, string raw, string name, MinecraftOptionCategory category, IEnumerable<MinecraftEnumOption> options) { var entry = new MinecraftOptionEntry { Key = key, DisplayName = name, Category = category, Type = MinecraftOptionType.Enum, RawValue = raw, EnumOptions = options.ToArray() }; entry.AcceptChanges(); return entry; }
    private static bool IsNumber(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    private static string Resolve(IReadOnlyDictionary<string, string> lang, string key, string fallback) => lang.TryGetValue(key, out var value) ? value : fallback;
    private static string ResolveEnumLabel(string key, string value, IReadOnlyDictionary<string, string> lang) => new[] { "options." + key + "." + value, "options." + value, "options.graphics." + value, "options.particles." + value, "options.narrator." + value }.Select(candidate => Resolve(lang, candidate, string.Empty)).FirstOrDefault(label => label.Length > 0) ?? Prettify(value);
    private static MinecraftOptionCategory CategoryFor(string key) => key switch { "chatVisibility" or "chatOpacity" or "chatLineSpacing" or "chatDelay" or "chatWidth" or "chatHeightFocused" or "chatHeightUnfocused" or "chatScale" or "textBackgroundOpacity" or "chatColors" or "chatLinks" or "chatLinksPrompt" or "backgroundForChatOnly" => MinecraftOptionCategory.Chat, "mouseSensitivity" or "invertYMouse" or "invertXMouse" or "discrete_mouse_scroll" or "autoJump" or "toggleCrouch" or "toggleSprint" or "toggleAttack" or "toggleUse" or "sprintWindow" or "mainHand" => MinecraftOptionCategory.Controls, "showSubtitles" or "highContrast" or "forceUnicodeFont" or "darkMojangStudiosBackground" or "menuBackgroundBlurriness" or "notificationDisplayTime" => MinecraftOptionCategory.Accessibility, "fullscreen" => MinecraftOptionCategory.Display, _ => MinecraftOptionCategory.Graphics };
    private static string ResolveKeyBindName(string key, string raw, IReadOnlyDictionary<string, string> lang) => Resolve(lang, key["key_".Length..], Prettify(key)) + " — " + Resolve(lang, raw, LegacyKey(raw));
    private static string LegacyKey(string raw) => raw switch { "-100" => "Mouse Left", "-99" => "Mouse Right", "-98" => "Mouse Middle", "17" => "W", "30" => "A", "31" => "S", "32" => "D", "57" => "Space", "42" => "Left Shift", "29" => "Left Control", _ => raw };
    private static string Prettify(string key) { var clean = key.StartsWith("key_key.", StringComparison.Ordinal) ? key["key_key.".Length..] : key.Replace('_', ' ').Replace('.', ' '); var builder = new StringBuilder(); for (var index = 0; index < clean.Length; index++) { var c = clean[index]; if (index > 0 && char.IsUpper(c) && char.IsLower(clean[index - 1])) builder.Append(' '); builder.Append(c); } return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(builder.ToString()); }
}
