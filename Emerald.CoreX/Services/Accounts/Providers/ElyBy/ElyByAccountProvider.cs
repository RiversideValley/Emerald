using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth.Authlib;
using Emerald.CoreX.Services.Auth.OAuth;

namespace Emerald.CoreX.Services.Auth.ElyBy;

/// <summary>Owns the full Ely.by credential, refresh, removal, and launch lifecycle.</summary>
internal sealed class ElyByAccountProvider(
    IElyByAccountStore accountStore,
    IElyByAuthClient authClient,
    IBrowserOAuthBroker browser,
    IAuthlibInjectorService authlibInjectorService,
    ElyByOAuthOptions oauthOptions,
    AccountProviderPolicyOptions policyOptions,
    global::Microsoft.Extensions.Logging.ILogger<ElyByAccountProvider> logger) : IAccountProvider
{
    public const string BrowserMethodId = "browser";

    public AccountProviderDescriptor Descriptor { get; } = new(
        AccountProviderIds.ElyBy,
        "Ely.by",
        [
            new AccountSignInMethodDescriptor(
                BrowserMethodId,
                "Sign in with Ely.by",
                "Use your default browser to sign in with Ely.by",
            IsDefault: true)],
        oauthOptions.IsConfigured,
        oauthOptions.IsConfigured ? null : "Ely.by OAuth is not configured for this build.",
        Actions: [new AccountProviderActionDescriptor(
            "manage-skins",
            "Manage skins",
            new Uri("https://ely.by/skins"))],
        Requirements: policyOptions.RequireMicrosoftForElyByAccounts
            ? [new AccountProviderRequirement(
                AccountProviderIds.Microsoft,
                "Add a Microsoft account before signing in to or selecting Ely.by.")]
            : []);

    public Task InitializeAsync(AccountProviderInitializationContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<AccountProviderLoadResult> LoadAccountsAsync(IReadOnlyList<EAccount> persistedAccounts, CancellationToken cancellationToken = default)
        => Task.FromResult(new AccountProviderLoadResult(accountStore.GetAccounts()
            .Select(ToAccount)
            .ToList()));

    public AccountProviderUsability GetAccountUsability(EAccount account)
    {
        var stored = accountStore.Find(account.UniqueId);
        if (stored is null)
            return new AccountProviderUsability(false, $"Ely.by account '{account.Name}' is no longer signed in.");
        if (Descriptor.IsConfigured || stored.AuthFlow == ElyByAuthFlow.Direct)
            return AccountProviderUsability.Available;
        return new AccountProviderUsability(false, Descriptor.ConfigurationMessage);
    }

    public async Task<EAccount> SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (request.MethodId != BrowserMethodId)
            throw new ArgumentException($"Unsupported Ely.by sign-in method '{request.MethodId}'.", nameof(request));

        var state = CreateOAuthState();
        var authorization = authClient.CreateOAuthAuthorizationRequest(state);
        var result = await browser.AuthorizeAsync(authorization, cancellationToken).ConfigureAwait(false);
        var session = await authClient.ExchangeOAuthCodeAsync(result.Code, cancellationToken).ConfigureAwait(false);
        var stored = ToStoredAccount(session);
        accountStore.Upsert(stored);
        return ToAccount(stored);
    }

    public async Task RefreshAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        var stored = accountStore.Find(account.UniqueId)
            ?? throw new InvalidOperationException($"Ely.by account '{account.Name}' is no longer signed in.");
        account.Availability = AccountAvailability.Refreshing;
        account.AvailabilityMessage = null;
        try
        {
            var session = await GetCurrentSessionAsync(stored, cancellationToken).ConfigureAwait(false);
            UpdateStoredAccount(stored, session);
            accountStore.Upsert(stored);
            UpdateAccount(account, stored);
            account.Availability = AccountAvailability.Ready;
        }
        catch (ElyByReauthenticationRequiredException ex)
        {
            account.Availability = AccountAvailability.ReauthenticationRequired;
            account.AvailabilityMessage = ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            account.Availability = AccountAvailability.Error;
            account.AvailabilityMessage = ex.Message;
            throw;
        }
    }

    public async Task<GameAuthenticationResult> AuthenticateForLaunchAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        await RefreshAsync(account, cancellationToken).ConfigureAwait(false);
        var stored = accountStore.Find(account.UniqueId)
            ?? throw new InvalidOperationException($"Ely.by account '{account.Name}' is no longer signed in.");
        var agent = await authlibInjectorService.GetJavaAgentArgumentAsync(cancellationToken).ConfigureAwait(false);
        return new GameAuthenticationResult(
            new MSession
            {
                Username = stored.Name,
                UUID = stored.UUID,
                AccessToken = stored.AccessToken,
                ClientToken = stored.ClientToken,
                UserType = "msa"
            },
            new AccountRuntimeAuthOptions([new MArgument(agent)]));
    }

    public async Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        var stored = accountStore.Find(account.UniqueId);
        if (stored is not null)
        {
            try { await authClient.InvalidateAsync(stored, cancellationToken).ConfigureAwait(false); }
            catch (Exception ex)
            {
                // Remote invalidation is best-effort. The local credential must
                // still be removed so the user can always sign out of Emerald.
                global::Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(
                    logger,
                    ex,
                    "Ely.by rejected remote invalidation for account {AccountId}; continuing local removal.",
                    account.UniqueId);
            }
        }
        accountStore.Remove(account.UniqueId);
    }

    private async Task<ElyByAuthSession> GetCurrentSessionAsync(ElyByStoredAccount stored, CancellationToken cancellationToken)
    {
        if (stored.AuthFlow == ElyByAuthFlow.OAuth)
        {
            if (stored.AccessTokenExpiresAt is { } expiresAt && expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
                return new ElyByAuthSession(stored.Name, stored.UUID, stored.AccessToken, stored.ClientToken, stored.RefreshToken, stored.AccessTokenExpiresAt, stored.AuthFlow);
            return await authClient.RefreshAsync(stored, cancellationToken).ConfigureAwait(false);
        }
        if (await authClient.ValidateAsync(stored.AccessToken, stored.ClientToken, cancellationToken).ConfigureAwait(false))
            return new ElyByAuthSession(stored.Name, stored.UUID, stored.AccessToken, stored.ClientToken);
        return await authClient.RefreshAsync(stored, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureConfigured()
    {
        if (!Descriptor.IsConfigured)
            throw new ElyByAuthException(Descriptor.ConfigurationMessage!);
    }

    private static EAccount ToAccount(ElyByStoredAccount stored)
    {
        var availability = stored.AuthFlow == ElyByAuthFlow.OAuth && stored.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5)
            ? AccountAvailability.NeedsRefresh
            : AccountAvailability.Ready;
        return new EAccount(stored.Name, AccountType.ElyBy, stored.UUID, stored.UniqueId)
        {
            LastUsed = stored.LastUsed,
            ProviderId = AccountProviderIds.ElyBy,
            ProviderDisplayName = "Ely.by",
            Availability = availability
        };
    }

    private static void UpdateAccount(EAccount account, ElyByStoredAccount stored)
    {
        account.Name = stored.Name;
        account.UUID = stored.UUID;
        account.LastUsed = stored.LastUsed;
        account.ProviderId = AccountProviderIds.ElyBy;
        account.ProviderDisplayName = "Ely.by";
    }

    private static ElyByStoredAccount ToStoredAccount(ElyByAuthSession session) => new()
    {
        UniqueId = $"{AccountProviderIds.ElyBy}:{session.UUID}",
        Name = session.Name,
        UUID = session.UUID,
        AccessToken = session.AccessToken,
        ClientToken = session.ClientToken,
        RefreshToken = session.RefreshToken ?? string.Empty,
        AccessTokenExpiresAt = session.AccessTokenExpiresAt,
        AuthFlow = session.AuthFlow,
        LastUsed = DateTime.UtcNow
    };

    private static void UpdateStoredAccount(ElyByStoredAccount stored, ElyByAuthSession session)
    {
        stored.Name = session.Name;
        stored.UUID = session.UUID;
        stored.AccessToken = session.AccessToken;
        stored.ClientToken = session.ClientToken;
        stored.RefreshToken = session.RefreshToken ?? stored.RefreshToken;
        stored.AccessTokenExpiresAt = session.AccessTokenExpiresAt;
        stored.AuthFlow = session.AuthFlow;
        stored.LastUsed = DateTime.UtcNow;
    }

    private static string CreateOAuthState()
    {
        Span<byte> bytes = stackalloc byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
