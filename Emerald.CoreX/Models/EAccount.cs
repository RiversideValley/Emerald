using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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

    public EAccount() { }

    public EAccount(string name, AccountType type, string uuid = "", string uniqueId = "")
    {
        Name = name;
        Type = type;
        UUID = uuid;
        UniqueId = uniqueId ?? Guid.NewGuid().ToString();
        LastUsed = DateTime.UtcNow;
    }
}
