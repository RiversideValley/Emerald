using CmlLib.Core.Auth;
using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth.Offline;

/// <summary>Owns offline account persistence, creation, and launch sessions.</summary>
internal sealed class OfflineAccountProvider(AccountProviderPolicyOptions policyOptions) : IAccountProvider
{
    public const string CreateMethodId = "username";

    public AccountProviderDescriptor Descriptor { get; } = new(
        AccountProviderIds.Offline,
        "Offline",
        [
            new AccountSignInMethodDescriptor(
                CreateMethodId,
                "Enter your username",
                "Enter a specific username for creating an offline Account",
                AccountSignInInputKind.Username, true)],
        Requirements: policyOptions.RequireMicrosoftForOfflineAccounts
            ? [new AccountProviderRequirement(
                AccountProviderIds.Microsoft,
                "Add a Microsoft account before creating or selecting an offline account.")]
            : [],
        RequiresNetworkForLaunch: false);

    public Task InitializeAsync(AccountProviderInitializationContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<AccountProviderLoadResult> LoadAccountsAsync(IReadOnlyList<EAccount> persistedAccounts, CancellationToken cancellationToken = default)
    {
        var accounts = persistedAccounts
            .Where(account => account.ProviderId == AccountProviderIds.Offline ||
                              (string.IsNullOrWhiteSpace(account.ProviderId) && account.Type == AccountType.Offline))
            .Select(account => new EAccount(account.Name, AccountType.Offline, account.UUID, account.UniqueId)
            {
                LastUsed = account.LastUsed,
                ProviderId = AccountProviderIds.Offline,
                ProviderDisplayName = Descriptor.DisplayName,
                Availability = AccountAvailability.Ready
            })
            .ToList();
        return Task.FromResult(new AccountProviderLoadResult(accounts));
    }

    public Task<EAccount> SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MethodId != CreateMethodId || string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException("An offline username is required.", nameof(request));

        return Task.FromResult(new EAccount(request.Username.Trim(), AccountType.Offline)
        {
            ProviderDisplayName = Descriptor.DisplayName
        });
    }

    public Task RefreshAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        account.Availability = AccountAvailability.Ready;
        account.AvailabilityMessage = null;
        return Task.CompletedTask;
    }

    public Task<AccountSkinData?> GetSkinAsync(EAccount account, CancellationToken cancellationToken = default)
        => Task.FromResult<AccountSkinData?>(MinecraftSkinTextures.CreateSteveFallback("Offline"));

    public Task<GameAuthenticationResult> AuthenticateForLaunchAsync(EAccount account, CancellationToken cancellationToken = default)
        => Task.FromResult(new GameAuthenticationResult(MSession.CreateOfflineSession(account.Name)));

    public Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
