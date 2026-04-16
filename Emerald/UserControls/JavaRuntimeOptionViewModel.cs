using Emerald.CoreX.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.UserControls;

public partial class JavaRuntimeOptionViewModel : ObservableObject
{
    public required string Path { get; init; }

    public required string DisplayPath { get; init; }

    public required string Source { get; init; }

    public string? Version { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsCustomSaved { get; init; }

    [NotifyPropertyChangedFor(nameof(CanSelect))]
    [ObservableProperty]
    private bool _isSelected;

    [NotifyPropertyChangedFor(nameof(CanSelect), nameof(IsInvalid), nameof(StatusText))]
    [ObservableProperty]
    private bool _isValid;

    public bool CanSelect => IsValid && !IsSelected;

    public bool IsInvalid => !IsValid;

    public string StatusText
        => IsValid
            ? Version ?? "JavaVersionUnavailable".Localize()
            : ErrorMessage ?? "JavaValidationFailedMessage".Localize();
}
