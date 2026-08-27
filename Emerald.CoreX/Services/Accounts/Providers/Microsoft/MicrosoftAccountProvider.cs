using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth.Microsoft;

/// <summary>Adapts CmlLib's Microsoft account store to Emerald's provider contract.</summary>
internal sealed class MicrosoftAccountProvider(IMicrosoftAccountClient client, string clientId) : IAccountProvider
{
    public const string BrowserMethodId = "browser";

    public AccountProviderDescriptor Descriptor { get; } = new(
        AccountProviderIds.Microsoft,
        "Microsoft",
        [
            new AccountSignInMethodDescriptor(
                BrowserMethodId,
                "Using browser",
                "Use your default browser to sign in with Microsoft",
                IsDefault: true)]);

    public Task InitializeAsync(AccountProviderInitializationContext context, CancellationToken cancellationToken = default)
        => client.InitializeAsync(clientId, context.AccountStorePath);

    public Task<AccountProviderLoadResult> LoadAccountsAsync(IReadOnlyList<EAccount> persistedAccounts, CancellationToken cancellationToken = default)
    {
        var accounts = client.GetAccounts()
            .Where(account => !string.IsNullOrWhiteSpace(account.Identifier))
            .Select(account => new EAccount(
                account.Name,
                AccountType.Microsoft,
                string.IsNullOrWhiteSpace(account.UUID) ? account.Identifier : account.UUID,
                account.Identifier)
            {
                LastUsed = account.LastAccess == default ? DateTime.UtcNow : account.LastAccess,
                ProviderId = AccountProviderIds.Microsoft,
                ProviderDisplayName = Descriptor.DisplayName,
                Availability = AccountAvailability.Ready
            })
            .ToList();
        var removedNames = persistedAccounts
            .Where(account => account.ProviderId == AccountProviderIds.Microsoft)
            .Where(stored => !accounts.Any(loaded =>
                string.Equals(loaded.UniqueId, stored.UniqueId, StringComparison.Ordinal)))
            .Select(account => account.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        IReadOnlyList<AccountProviderNotice> notices = removedNames.Count switch
        {
            0 => [],
            1 => [new(
                "Microsoft account signed out",
                $"'{removedNames[0]}' is no longer signed in and was removed from Accounts.")],
            _ => [new(
                "Microsoft accounts signed out",
                $"{removedNames.Count} Microsoft accounts are no longer signed in and were removed from Accounts.")]
        };

        return Task.FromResult(new AccountProviderLoadResult(accounts, notices));
    }

    public async Task<EAccount> SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MethodId != BrowserMethodId)
            throw new ArgumentException($"Unsupported Microsoft sign-in method '{request.MethodId}'.", nameof(request));

        var before = client.GetAccounts().Select(account => account.Identifier).ToHashSet(StringComparer.Ordinal);
        var result = await client.SignInInteractivelyAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var accounts = client.GetAccounts();
        var identifier = new[] { result.Identifier, result.UUID, client.GetDefaultAccountIdentifier() }
            .Concat(accounts.Where(account => !before.Contains(account.Identifier))
                .OrderByDescending(account => account.LastAccess)
                .Select(account => account.Identifier))
            .Concat(accounts.OrderByDescending(account => account.LastAccess).Select(account => account.Identifier))
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        var account = accounts.FirstOrDefault(candidate => string.Equals(candidate.Identifier, identifier, StringComparison.Ordinal));
        if (account is null)
            throw new InvalidOperationException("Microsoft sign-in completed, but Emerald could not materialize the signed-in account.");

        return new EAccount(account.Name, AccountType.Microsoft, account.UUID, account.Identifier)
        {
            LastUsed = account.LastAccess == default ? DateTime.UtcNow : account.LastAccess,
            ProviderId = AccountProviderIds.Microsoft,
            ProviderDisplayName = Descriptor.DisplayName,
            Availability = AccountAvailability.Ready
        };
    }

    public async Task RefreshAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        account.Availability = AccountAvailability.Refreshing;
        try
        {
            await client.AuthenticateAsync(account.UniqueId).WaitAsync(cancellationToken).ConfigureAwait(false);
            account.Availability = AccountAvailability.Ready;
            account.AvailabilityMessage = null;
        }
        catch (Exception ex)
        {
            account.Availability = AccountAvailability.ReauthenticationRequired;
            account.AvailabilityMessage = ex.Message;
            throw;
        }
    }

    public async Task<GameAuthenticationResult> AuthenticateForLaunchAsync(EAccount account, CancellationToken cancellationToken = default)
        => new(await client.AuthenticateAsync(account.UniqueId).WaitAsync(cancellationToken).ConfigureAwait(false));

    public Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
        => client.SignOutAsync(account.UniqueId);
}
