using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth;

/// <summary>
/// Owns the complete lifecycle for one account system. AccountService only
/// coordinates providers and persists shared account metadata.
/// </summary>
public interface IAccountProvider
{
    /// <summary>Describes the provider, its sign-in methods, and its dependencies.</summary>
    AccountProviderDescriptor Descriptor { get; }

    /// <summary>Initializes provider storage or SDK state.</summary>
    Task InitializeAsync(AccountProviderInitializationContext context, CancellationToken cancellationToken = default);

    /// <summary>Loads provider accounts and any notices produced by reconciliation.</summary>
    Task<AccountProviderLoadResult> LoadAccountsAsync(
        IReadOnlyList<EAccount> persistedAccounts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates provider-specific usability for an existing account. Provider
    /// configuration can disable new sign-in without necessarily stranding legacy accounts.
    /// </summary>
    AccountProviderUsability GetAccountUsability(EAccount account)
        => AccountProviderUsability.Available;

    Task<EAccount> SignInAsync(
        AccountSignInRequest request,
        CancellationToken cancellationToken = default);

    Task RefreshAsync(EAccount account, CancellationToken cancellationToken = default);

    Task<GameAuthenticationResult> AuthenticateForLaunchAsync(
        EAccount account,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default);
}

/// <summary>Shared initialization data that is useful to account providers.</summary>
public sealed record AccountProviderInitializationContext(string AccountStorePath);

/// <summary>Static metadata used by account orchestration and the Accounts page.</summary>
public sealed record AccountProviderDescriptor(
    string ProviderId,
    string DisplayName,
    IReadOnlyList<AccountSignInMethodDescriptor> SignInMethods,
    bool IsConfigured = true,
    string? ConfigurationMessage = null,
    IReadOnlyList<AccountProviderActionDescriptor>? Actions = null,
    IReadOnlyList<AccountProviderRequirement>? Requirements = null,
    bool RequiresNetworkForLaunch = true)
{
    public IReadOnlyList<AccountProviderActionDescriptor> EffectiveActions { get; }
        = Actions ?? [];
    public IReadOnlyList<AccountProviderRequirement> EffectiveRequirements { get; }
        = Requirements ?? [];
}

/// <summary>An external provider-owned action rendered without service-specific code.</summary>
public sealed record AccountProviderActionDescriptor(string ActionId, string DisplayName, Uri Uri);

/// <summary>Requires at least one account from another provider.</summary>
public sealed record AccountProviderRequirement(string ProviderId, string UnavailableMessage);

/// <summary>Result of evaluating configuration and account dependencies.</summary>
public sealed record AccountProviderUsability(bool IsAvailable, string? UnavailableReason)
{
    public static AccountProviderUsability Available { get; } = new(true, null);
}

/// <summary>Accounts and user-facing reconciliation notices returned during load.</summary>
public sealed record AccountProviderLoadResult(
    IReadOnlyList<EAccount> Accounts,
    IReadOnlyList<AccountProviderNotice> Notices)
{
    public AccountProviderLoadResult(IReadOnlyList<EAccount> accounts)
        : this(accounts, [])
    {
    }
}

public sealed record AccountProviderNotice(string Title, string Message);

/// <summary>Configures cross-provider account requirements without coupling them to AccountService.</summary>
public sealed record AccountProviderPolicyOptions
{
    public bool RequireMicrosoftForOfflineAccounts { get; init; } = true;
    public bool RequireMicrosoftForElyByAccounts { get; init; } = true;
}

public sealed record AccountSignInMethodDescriptor(
    string MethodId,
    string DisplayName,
    string Description,
    AccountSignInInputKind InputKind = AccountSignInInputKind.None,
    bool IsDefault = false);

public enum AccountSignInInputKind
{
    None,
    Username
}

public sealed record AccountSignInRequest(string MethodId, string? Username = null);

public enum AccountAvailability
{
    Ready,
    NeedsRefresh,
    Refreshing,
    ReauthenticationRequired,
    Error
}
