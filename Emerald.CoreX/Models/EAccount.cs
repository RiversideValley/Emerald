using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.CoreX.Models;

public enum AccountType
{
    Offline,
    Microsoft
}

[ObservableObject]
public partial class EAccount
{
    [ObservableProperty]

    private string _name = string.Empty;
    [ObservableProperty]
    private AccountType _type;

    [ObservableProperty]
    private string _UUID = string.Empty;

    [ObservableProperty]
    private DateTime _lastUsed;

    [ObservableProperty]
    private string _uniqueId = string.Empty;

    [JsonIgnore]
    [ObservableProperty]
    private bool _isSelected;

    public EAccount() { }

    public EAccount(string name, AccountType type, string uuid = "", string uniqueId = "")
    {
        Name = name;
        Type = type;
        UUID = uuid;
        UniqueId = string.IsNullOrWhiteSpace(uniqueId)
            ? Guid.NewGuid().ToString()
            : uniqueId;
        LastUsed = DateTime.UtcNow;
    }
}
