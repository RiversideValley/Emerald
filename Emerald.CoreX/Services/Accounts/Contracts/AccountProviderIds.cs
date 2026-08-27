using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth;

public static class AccountProviderIds
{
    // These IDs are serialized. Never rename one without a settings migration.
    public const string Offline = "offline";
    public const string Microsoft = "microsoft";
    public const string ElyBy = "elyby";

    public static string FromAccountType(AccountType type)
        => type switch
        {
            AccountType.Offline => Offline,
            AccountType.Microsoft => Microsoft,
            AccountType.ElyBy => ElyBy,
            _ => string.Empty
        };

    public static string GetDisplayName(string? providerId)
        => providerId switch
        {
            Offline => "Offline",
            Microsoft => "Microsoft",
            ElyBy => "Ely.by",
            _ => string.IsNullOrWhiteSpace(providerId) ? "Unknown" : providerId
        };
}
