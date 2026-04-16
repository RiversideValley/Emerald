using Emerald.CoreX.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.UserControls;

public partial class JavaRuntimeOptionViewModel : ObservableObject
{
    public string Path { get; set; } = string.Empty;

    public string DisplayPath { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsCustomSaved { get; set; }

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
