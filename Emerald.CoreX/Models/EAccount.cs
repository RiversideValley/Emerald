using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Services.Auth;

namespace Emerald.CoreX.Models;

/// <summary>
/// Legacy serialized account kind. New orchestration must use <see cref="EAccount.ProviderId"/>
/// so adding a provider never requires another enum value.
/// </summary>
public enum AccountType
{
    Offline,
    Microsoft,
    ElyBy,
    /// <summary>
    /// Compatibility bucket for providers that are not built in to Emerald.
    /// ProviderId is the canonical identity for all new providers.
    /// </summary>
    Other
}

[ObservableObject]
public partial class EAccount
{
    [ObservableProperty]
    private string _name = string.Empty;

    // Retained for settings compatibility; ProviderId is the canonical provider identity.
    [ObservableProperty]
    private AccountType _type;

    [ObservableProperty]
    private string _UUID = string.Empty;

    [ObservableProperty]
    private DateTime _lastUsed;

    [ObservableProperty]
    private string _uniqueId = string.Empty;

    [ObservableProperty]
    private string _providerId = string.Empty;

    [JsonIgnore]
    [ObservableProperty]
    private bool _isSelected;

    [JsonIgnore]
    [ObservableProperty]
    private string _providerDisplayName = string.Empty;

    [JsonIgnore]
    [ObservableProperty]
    private AccountAvailability _availability = AccountAvailability.Ready;

    [JsonIgnore]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailabilityMessage))]
    private string? _availabilityMessage;

    [JsonIgnore]
    [ObservableProperty]
    private IReadOnlyList<AccountProviderActionDescriptor> _providerActions = [];

    public bool HasAvailabilityMessage => !string.IsNullOrWhiteSpace(AvailabilityMessage);

    public EAccount() { }

    public EAccount(string name, AccountType type, string uuid = "", string uniqueId = "")
    {
        Name = name;
        Type = type;
        UUID = uuid;
        UniqueId = string.IsNullOrWhiteSpace(uniqueId)
            ? Guid.NewGuid().ToString()
            : uniqueId;
        ProviderId = AccountProviderIds.FromAccountType(type);
        ProviderDisplayName = AccountProviderIds.GetDisplayName(ProviderId);
        LastUsed = DateTime.UtcNow;
    }
}
