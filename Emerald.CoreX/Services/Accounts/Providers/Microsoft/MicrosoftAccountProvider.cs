using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth.Microsoft;

/// <summary>Adapts CmlLib's Microsoft account store to Emerald's provider contract.</summary>
internal sealed class MicrosoftAccountProvider(IMicrosoftAccountClient client, string clientId, HttpClient? skinHttpClient = null) : IAccountProvider
{
    public const string BrowserMethodId = "browser";
    private const string MissingConfigurationMessage = "Microsoft sign-in is not configured for this build.";

    public AccountProviderDescriptor Descriptor { get; } = new(
        AccountProviderIds.Microsoft,
        "Microsoft",
        [
            new AccountSignInMethodDescriptor(
                BrowserMethodId,
                "Using browser",
                "Use your default browser to sign in with Microsoft",
                IsDefault: true)],
        IsConfigured: !string.IsNullOrWhiteSpace(clientId),
        ConfigurationMessage: string.IsNullOrWhiteSpace(clientId) ? MissingConfigurationMessage : null);

    public Task InitializeAsync(AccountProviderInitializationContext context, CancellationToken cancellationToken = default)
        => Descriptor.IsConfigured
            ? client.InitializeAsync(clientId, context.AccountStorePath)
            : Task.CompletedTask;

    public Task<AccountProviderLoadResult> LoadAccountsAsync(IReadOnlyList<EAccount> persistedAccounts, CancellationToken cancellationToken = default)
    {
        if (!Descriptor.IsConfigured)
        {
            var persistedMicrosoftAccounts = persistedAccounts
                .Where(IsMicrosoftAccount)
                .ToList();

            foreach (var account in persistedMicrosoftAccounts)
            {
                account.Availability = AccountAvailability.Error;
                account.AvailabilityMessage = MissingConfigurationMessage;
            }

            return Task.FromResult(new AccountProviderLoadResult(persistedMicrosoftAccounts));
        }

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
        EnsureConfigured();

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
        EnsureConfigured();
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
    {
        EnsureConfigured();
        return new(await client.AuthenticateAsync(account.UniqueId).WaitAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<AccountSkinData?> GetSkinAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        var skin = client.GetAccounts().FirstOrDefault(candidate =>
            string.Equals(candidate.Identifier, account.UniqueId, StringComparison.Ordinal));
        if (skin is null || !Uri.TryCreate(skin.SkinUrl, UriKind.Absolute, out var skinUri))
            return null;

        return await AccountSkinDownload.DownloadAsync(
            skinHttpClient ?? new HttpClient(), skinUri, skin.SkinVariant, Descriptor.DisplayName, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
        => Descriptor.IsConfigured
            ? client.SignOutAsync(account.UniqueId)
            : Task.CompletedTask;

    public AccountProviderUsability GetAccountUsability(EAccount account)
        => Descriptor.IsConfigured
            ? AccountProviderUsability.Available
            : new AccountProviderUsability(false, MissingConfigurationMessage);

    private static bool IsMicrosoftAccount(EAccount account)
        => string.Equals(account.ProviderId, AccountProviderIds.Microsoft, StringComparison.Ordinal)
           || (string.IsNullOrWhiteSpace(account.ProviderId) && account.Type == AccountType.Microsoft);

    private void EnsureConfigured()
    {
        if (!Descriptor.IsConfigured)
            throw new InvalidOperationException(MissingConfigurationMessage);
    }
}
