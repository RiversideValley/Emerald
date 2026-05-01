using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Services.Auth;

namespace Emerald.CoreX.Models;

public enum AccountType
{
    Offline,
    Microsoft,
    ElyBy
}

[ObservableObject]
public partial class EAccount
{
    [ObservableProperty]

    private string _name = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProviderDisplayName))]
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
    public string ProviderDisplayName => Type switch
    {
        AccountType.Microsoft => "Microsoft",
        AccountType.ElyBy => "Ely.by",
        _ => "Offline"
    };

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
        LastUsed = DateTime.UtcNow;
    }
}
