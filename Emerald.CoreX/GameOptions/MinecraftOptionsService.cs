using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.GameOptions;

public sealed class MinecraftOptionsService : IMinecraftOptionsService
{
    // ── Keys to hide from the editor ─────────────────────────────────────────
    private static readonly HashSet<string> SkippedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "version", "lang", "lastServer", "resourcePacks", "incompatibleResourcePacks",
        "soundDevice", "tutorialStep", "startedCleanly", "realmsNotifications",
        "joinedFirstServer", "hideServerAddress", "advanced_itemtooltips",
        "pauseOnLostFocus", "log4jFixedProtocolVersion", "enableVsync",
        "glDebugVerbosity", "skipMultiplayerWarning", "skipRealmsWarning",
        "hideMatchedNames", "chatLinks", "chatLinksPrompt"
    };

    // ── Known slider ranges ───────────────────────────────────────────────────
    private static readonly Dictionary<string, (double Min, double Max, bool IsInt, double Step, string? Suffix)>
        SliderMeta = new(StringComparer.OrdinalIgnoreCase)
        {
            ["renderDistance"]           = (2,  32,  true,  1,    " chunks"),
            ["simulationDistance"]       = (5,  32,  true,  1,    " chunks"),
            ["maxFps"]                   = (10, 260, true,  1,    " fps"),
            ["fov"]                      = (30, 110, false, 1,    "°"),
            ["gamma"]                    = (0,  1,   false, 0.01, null),
            ["fovEffectScale"]           = (0,  1,   false, 0.01, null),
            ["mouseSensitivity"]         = (0,  1,   false, 0.01, null),
            ["mipmapLevels"]             = (0,  4,   true,  1,    null),
            ["biomeBlendRadius"]         = (0,  7,   true,  1,    null),
            ["screenEffectScale"]        = (0,  1,   false, 0.01, null),
            ["chatOpacity"]              = (0,  1,   false, 0.01, null),
            ["chatLineSpacing"]          = (0,  1,   false, 0.01, null),
            ["chatDelay"]                = (0,  6,   false, 0.1,  "s"),
            ["chatWidth"]                = (0,  1,   false, 0.01, null),
            ["chatHeightFocused"]        = (0,  1,   false, 0.01, null),
            ["chatHeightUnfocused"]      = (0,  1,   false, 0.01, null),
            ["chatScale"]                = (0,  1,   false, 0.01, null),
            ["textBackgroundOpacity"]    = (0,  1,   false, 0.01, null),
            ["notificationDisplayTime"]  = (0.5, 5, false, 0.5,  "s"),
            ["menuBackgroundBlurriness"] = (0,  10,  true,  1,    null),
            ["entityDistanceScaling"]    = (0.5, 5, false, 0.1,  "×"),
        };

    // ── Category heuristics ───────────────────────────────────────────────────
    private static readonly Dictionary<string, string> CategoryMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["renderDistance"]          = "Graphics",
            ["simulationDistance"]      = "Graphics",
            ["maxFps"]                  = "Graphics",
            ["fov"]                     = "Graphics",
            ["gamma"]                   = "Graphics",
            ["graphicsMode"]            = "Graphics",
            ["ao"]                      = "Graphics",
            ["fancyGraphics"]           = "Graphics",
            ["graphics"]                = "Graphics",
            ["particles"]               = "Graphics",
            ["entityShadows"]           = "Graphics",
            ["renderClouds"]            = "Graphics",
            ["mipmapLevels"]            = "Graphics",
            ["vsync"]                   = "Graphics",
            ["entityDistanceScaling"]   = "Graphics",
            ["chunkBuilderType"]        = "Graphics",
            ["prioritizeChunkUpdates"]  = "Graphics",
            ["fullscreen"]              = "Display",
            ["guiScale"]                = "Display",
            ["fullscreenResolution"]    = "Display",
            ["narrator"]                = "Accessibility",
            ["subtitles"]               = "Accessibility",
            ["highContrast"]            = "Accessibility",
            ["darkMojangStudiosBackground"] = "Accessibility",
            ["menuBackgroundBlurriness"] = "Accessibility",
            ["mouseSensitivity"]        = "Mouse & Controls",
            ["invertYMouse"]            = "Mouse & Controls",
            ["discreteMouseScroll"]     = "Mouse & Controls",
            ["smoothCamera"]            = "Mouse & Controls",
            ["touchscreen"]             = "Mouse & Controls",
            ["chatVisibility"]          = "Chat",
            ["chatColors"]              = "Chat",
            ["chatOpacity"]             = "Chat",
            ["chatLineSpacing"]         = "Chat",
            ["chatScale"]               = "Chat",
            ["chatWidth"]               = "Chat",
            ["chatHeightFocused"]       = "Chat",
            ["chatHeightUnfocused"]     = "Chat",
            ["chatDelay"]               = "Chat",
            ["textBackgroundOpacity"]   = "Chat",
        };

    private readonly ILogger<MinecraftOptionsService> _logger;

    public MinecraftOptionsService(ILogger<MinecraftOptionsService> logger)
        => _logger = logger;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<MinecraftOptionsLoadResult> LoadAsync(
        Game game, CancellationToken cancellationToken = default)
    {
        var optionsPath = Path.Combine(game.Path.BasePath, "options.txt");
        if (!File.Exists(optionsPath))
        {
            _logger.LogInformation(
                "options.txt not found at {Path} — game hasn't been launched yet.", optionsPath);
            return new MinecraftOptionsLoadResult { OptionsFileExists = false };
        }

        var raw  = await ParseOptionsFileAsync(optionsPath, cancellationToken);
        var lang = await TryLoadLangAsync(game, cancellationToken);
        var entries = BuildEntries(raw, lang);

        _logger.LogInformation(
            "Loaded {Count} option entries for {Name}.", entries.Count, game.Version.DisplayName);

        return new MinecraftOptionsLoadResult
        {
            Entries = entries,
            OptionsFileExists = true,
            OriginalRaw = raw
        };
    }

    public async Task SaveAsync(
        Game game, IEnumerable<MinecraftOptionEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var optionsPath = Path.Combine(game.Path.BasePath, "options.txt");

        // Re-read the file so we preserve lines we never showed in the UI.
        var original = File.Exists(optionsPath)
            ? await ParseOptionsFileAsync(optionsPath, cancellationToken)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
            original[e.Key] = e.RawValue;

        var lines = original.Select(kvp => $"{kvp.Key}:{kvp.Value}");
        await File.WriteAllLinesAsync(optionsPath, lines, cancellationToken);
        _logger.LogInformation("Saved options.txt for {Name}.", game.Version.DisplayName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parsing
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, string>> ParseOptionsFileAsync(
        string path, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in await File.ReadAllLinesAsync(path, ct))
        {
            var idx = line.IndexOf(':');
            if (idx < 1) continue;
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..]; // preserve value exactly
            if (!string.IsNullOrEmpty(key))
                map[key] = val;
        }
        return map;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lang loading
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, string>> TryLoadLangAsync(Game game, CancellationToken ct)
    {
        try
        {
            // Use the *vanilla* JAR (BasedOn), even for modded instances.
            var jarPath = Path.Combine(
                game.Path.Versions,
                game.Version.BasedOn,
                $"{game.Version.BasedOn}.jar");

            if (!File.Exists(jarPath))
            {
                _logger.LogDebug("JAR not found at {Path}; will prettify keys instead.", jarPath);
                return [];
            }

            return await LoadLangFromJarAsync(jarPath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load lang for {Name}.", game.Version.DisplayName);
            return [];
        }
    }

    private static async Task<Dictionary<string, string>> LoadLangFromJarAsync(
        string jarPath, CancellationToken ct)
    {
        using var jar = ZipFile.OpenRead(jarPath);

        // 1.13+ JSON
        var jsonEntry = jar.GetEntry("assets/minecraft/lang/en_us.json");
        if (jsonEntry is not null)
        {
            await using var stream = jsonEntry.Open();
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
        }

        // Pre-1.13 .lang
        var legacyEntry = jar.GetEntry("assets/minecraft/lang/en_US.lang");
        if (legacyEntry is null) return [];

        var lang = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var legacyStream = legacyEntry.Open();
        using var reader = new StreamReader(legacyStream);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            var eq = line.IndexOf('=');
            if (eq > 0) lang[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return lang;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Entry building
    // ─────────────────────────────────────────────────────────────────────────

    private List<MinecraftOptionEntry> BuildEntries(
        Dictionary<string, string> raw,
        Dictionary<string, string> lang)
    {
        var list = new List<MinecraftOptionEntry>();

        foreach (var (key, rawValue) in raw)
        {
            var type = ClassifyOption(key, rawValue);
            if (type == MinecraftOptionType.Skip) continue;

            var (min, max, isInt, step, suffix) = GetSliderMeta(key, type);

            list.Add(new MinecraftOptionEntry
            {
                Key          = key,
                DisplayName  = ResolveDisplayName(key, lang),
                Category     = ResolveCategory(key),
                Type         = type,
                RawValue     = rawValue,
                SliderMin    = min,
                SliderMax    = max,
                SliderIsInt  = isInt,
                SliderStep   = step,
                SliderSuffix = suffix,
                EnumOptions  = type == MinecraftOptionType.Enum
                    ? ResolveEnumOptions(key, rawValue, lang)
                    : []
            });
        }

        return list
            .OrderBy(e => e.Category)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Classification
    // ─────────────────────────────────────────────────────────────────────────

    private static MinecraftOptionType ClassifyOption(string key, string rawValue)
    {
        if (SkippedKeys.Contains(key))                                return MinecraftOptionType.Skip;
        if (key.StartsWith("key_",           StringComparison.OrdinalIgnoreCase)) return MinecraftOptionType.KeyBind;
        if (key.StartsWith("soundCategory_", StringComparison.OrdinalIgnoreCase)) return MinecraftOptionType.SoundVolume;

        if (rawValue is "true" or "false")     return MinecraftOptionType.Boolean;
        if (rawValue.StartsWith('"'))          return MinecraftOptionType.Enum;

        if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return MinecraftOptionType.Enum; // unrecognised string → treat as enum

        // Known int sliders
        if (SliderMeta.TryGetValue(key, out var meta) && meta.IsInt)
            return MinecraftOptionType.IntSlider;

        // Heuristic: no decimal point and value looks like a small int → int slider
        if (!rawValue.Contains('.') && d >= 0 && d <= 512)
            return MinecraftOptionType.IntSlider;

        return MinecraftOptionType.FloatSlider;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string ResolveDisplayName(string key, Dictionary<string, string> lang)
    {
        if (key.StartsWith("soundCategory_", StringComparison.OrdinalIgnoreCase))
        {
            var cat = key["soundCategory_".Length..];
            return lang.TryGetValue($"soundCategory.{cat}", out var n) ? n : PrettifyKey(cat);
        }
        return lang.TryGetValue($"options.{key}", out var name) ? name : PrettifyKey(key);
    }

    private static string ResolveCategory(string key)
    {
        if (key.StartsWith("key_",           StringComparison.OrdinalIgnoreCase)) return "Key Bindings";
        if (key.StartsWith("soundCategory_", StringComparison.OrdinalIgnoreCase)) return "Sound";
        return CategoryMap.TryGetValue(key, out var cat) ? cat : "General";
    }

    private static (double Min, double Max, bool IsInt, double Step, string? Suffix)
        GetSliderMeta(string key, MinecraftOptionType type)
    {
        if (type == MinecraftOptionType.SoundVolume) return (0, 1, false, 0.01, null);
        if (SliderMeta.TryGetValue(key, out var m))  return m;
        return type == MinecraftOptionType.IntSlider
            ? (0, 100, true,  1,    null)
            : (0, 1,   false, 0.01, null);
    }

    private static IReadOnlyList<MinecraftEnumOption> ResolveEnumOptions(
        string key, string rawValue, Dictionary<string, string> lang)
    {
        var opts  = new List<MinecraftEnumOption>();
        var pfx   = $"options.{key}.";

        foreach (var kvp in lang)
        {
            if (kvp.Key.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
                opts.Add(new MinecraftEnumOption(kvp.Key[pfx.Length..], kvp.Value));
        }

        // Fall back to shared labels (options.on / options.off)
        if (opts.Count == 0)
        {
            var shared = new[] { "true", "false", "on", "off" };
            foreach (var s in shared)
                if (lang.TryGetValue($"options.{s}", out var lbl))
                    opts.Add(new MinecraftEnumOption(s, lbl));
        }

        // Always include the current raw value so the ComboBox has something to select.
        var bare = rawValue.Trim('"');
        if (!string.IsNullOrEmpty(bare) && opts.All(o => o.RawValue != bare))
            opts.Insert(0, new MinecraftEnumOption(bare, PrettifyKey(bare)));

        return opts;
    }

    private static string PrettifyKey(string key)
    {
        // "key_key.forward" → "Forward"
        if (key.StartsWith("key_key.", StringComparison.OrdinalIgnoreCase))
            key = key["key_key.".Length..];

        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (c is '_' or '.') { sb.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(key[i - 1])) sb.Append(' ');
            sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return sb.ToString().Trim();
    }
}
