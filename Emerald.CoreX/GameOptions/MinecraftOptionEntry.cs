using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.CoreX.GameOptions;

public partial class MinecraftOptionEntry : ObservableObject
{
    public required string Key        { get; init; }
    public required string DisplayName { get; init; }
    public required MinecraftOptionType Type { get; init; }
    public MinecraftOptionCategory Category { get; init; } = MinecraftOptionCategory.General;
    public double   SliderMin { get; init; }
    public double   SliderMax { get; init; } = 1.0;
    public double   SliderStep { get; init; } = 0.01;
    public bool     SliderIsInt { get; init; }
    public string?  SliderSuffix { get; init; }
    /// <summary>Maps the stored value to the slider's display value.</summary>
    public double SliderStorageMultiplier { get; init; } = 1;
    public double SliderStorageOffset { get; init; }
    public IReadOnlyList<MinecraftEnumOption> EnumOptions { get; init; } = [];

    [ObservableProperty]
    private string _rawValue = string.Empty;

    public string OriginalRawValue { get; private set; } = string.Empty;
    public bool IsEditable => Type is not MinecraftOptionType.KeyBind and not MinecraftOptionType.ReadOnly and not MinecraftOptionType.Skip;
    public bool IsDirty => !string.Equals(RawValue, OriginalRawValue, System.StringComparison.Ordinal);

    partial void OnRawValueChanged(string value)
    {
        OnPropertyChanged(nameof(IsBooleanTrue));
        OnPropertyChanged(nameof(SliderValue));
        OnPropertyChanged(nameof(EnumRawValue));
        OnPropertyChanged(nameof(SelectedEnumOption));
        OnPropertyChanged(nameof(DisplayValueLabel));
        OnPropertyChanged(nameof(IsDirty));
    }

    // ── Boolean ──────────────────────────────────────────────────────────────
    public bool IsBooleanTrue
    {
        get => RawValue == "true";
        set => RawValue = value ? "true" : "false";
    }

    // ── Slider ───────────────────────────────────────────────────────────────
    public double SliderValue
    {
        get => double.TryParse(RawValue, NumberStyles.Float,
                   CultureInfo.InvariantCulture, out var d) ? (d * SliderStorageMultiplier) + SliderStorageOffset : SliderMin;
        set
        {
            var rounded = SliderIsInt ? Math.Round(value) : value;
            var stored = (rounded - SliderStorageOffset) / SliderStorageMultiplier;
            RawValue = SliderIsInt && SliderStorageMultiplier == 1 && SliderStorageOffset == 0
                ? ((int)rounded).ToString(CultureInfo.InvariantCulture)
                : stored.ToString("G17", CultureInfo.InvariantCulture);
            OnPropertyChanged();
        }
    }

    // ── Enum ─────────────────────────────────────────────────────────────────
    public string? EnumRawValue
    {
        get => RawValue;
        set
        {
            RawValue = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public MinecraftEnumOption? SelectedEnumOption
    {
        get => EnumOptions.FirstOrDefault(o => o.RawValue == EnumRawValue);
        set
        {
            if (value is not null) EnumRawValue = value.RawValue;
            OnPropertyChanged();
        }
    }

    // ── Display label ─────────────────────────────────────────────────────────
    public string DisplayValueLabel => Type switch
    {
        MinecraftOptionType.Boolean     => IsBooleanTrue ? "On" : "Off",
        MinecraftOptionType.SoundVolume => $"{(int)(SliderValue * 100)}%",
        MinecraftOptionType.IntSlider   => SliderSuffix is null
            ? $"{(int)SliderValue}"
            : $"{(int)SliderValue}{SliderSuffix}",
        MinecraftOptionType.FloatSlider => SliderSuffix is null
            ? $"{SliderValue:F2}"
            : $"{SliderValue:F1}{SliderSuffix}",
        MinecraftOptionType.Enum => EnumOptions
            .FirstOrDefault(o => o.RawValue == EnumRawValue)?.DisplayLabel
            ?? EnumRawValue
            ?? RawValue,
        MinecraftOptionType.KeyBind => RawValue,
        _ => RawValue
    };

    public void AcceptChanges()
    {
        OriginalRawValue = RawValue;
        OnPropertyChanged(nameof(IsDirty));
    }
}

public sealed record MinecraftEnumOption(string RawValue, string DisplayLabel);
