using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth;

public static class AccountProviderIds
{
    public const string Offline = "offline";
    public const string Microsoft = "microsoft";
    public const string ElyBy = "elyby";

    public static string FromAccountType(AccountType type)
        => type switch
        {
            AccountType.Offline => Offline,
            AccountType.Microsoft => Microsoft,
            AccountType.ElyBy => ElyBy,
            _ => type.ToString()
        };
}
