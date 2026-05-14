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
    public string   Category  { get; init; } = "General";
    public double   SliderMin { get; init; }
    public double   SliderMax { get; init; } = 1.0;
    public double   SliderStep { get; init; } = 0.01;
    public bool     SliderIsInt { get; init; }
    public string?  SliderSuffix { get; init; }
    public IReadOnlyList<MinecraftEnumOption> EnumOptions { get; init; } = [];

    [ObservableProperty]
    private string _rawValue = string.Empty;

    partial void OnRawValueChanged(string value)
    {
        OnPropertyChanged(nameof(IsBooleanTrue));
        OnPropertyChanged(nameof(SliderValue));
        OnPropertyChanged(nameof(EnumRawValue));
        OnPropertyChanged(nameof(SelectedEnumOption));
        OnPropertyChanged(nameof(DisplayValueLabel));
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
                   CultureInfo.InvariantCulture, out var d) ? d : SliderMin;
        set
        {
            var rounded = SliderIsInt ? Math.Round(value) : value;
            RawValue = SliderIsInt
                ? ((int)rounded).ToString(CultureInfo.InvariantCulture)
                : rounded.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged();
        }
    }

    // ── Enum ─────────────────────────────────────────────────────────────────
    public string? EnumRawValue
    {
        get => RawValue.Trim('"');
        set
        {
            RawValue = value is null ? string.Empty : $"\"{value}\"";
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
        MinecraftOptionType.KeyBind => RawValue.TrimStart("key.".ToCharArray()),
        _ => RawValue
    };
}

public sealed record MinecraftEnumOption(string RawValue, string DisplayLabel);
