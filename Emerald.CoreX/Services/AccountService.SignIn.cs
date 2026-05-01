using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth.Microsoft;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    public void CreateOfflineAccount(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));

        EnsureOfflineAccountPolicyMet("Creating offline accounts requires at least one Microsoft account.");

        _gate.Wait();
        try
        {
            _uiDispatcher.Invoke(() =>
            {
                if (_accounts.Any(account => account.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"An account named '{username}' already exists.");

                var account = new EAccount(username, AccountType.Offline);
                _accounts.Add(account);

                if (GetSelectedAccountCore() is null)
                    ApplySelectedAccountCore(account.UniqueId, persist: false);
            });
        }
        finally
        {
            _gate.Release();
        }

        PersistAccounts();
        _logger.LogInformation("Created offline account '{Username}'.", username);
    }

    public async Task SignInMicrosoftAccountAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        _logger.LogInformation("Starting interactive Microsoft sign-in.");
        var beforeIdentifiers = _microsoftAccountClient
            .GetAccounts()
            .Select(account => account.Identifier)
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .ToHashSet(StringComparer.Ordinal);

        var signInResult = await _microsoftAccountClient.SignInInteractivelyAsync().ConfigureAwait(false);
        _logger.LogInformation(
            "Interactive Microsoft sign-in completed for '{Username}' (candidate identifier: {Identifier}).",
            signInResult.Username ?? "Unknown",
            signInResult.Identifier ?? "None");

        var afterAccounts = _microsoftAccountClient.GetAccounts();
        await LoadAllAccountsAsync().ConfigureAwait(false);

        var candidateIdentifiers = BuildMaterializationCandidates(
            signInResult,
            beforeIdentifiers,
            afterAccounts,
            _microsoftAccountClient.GetDefaultAccountIdentifier());

        EAccount? materializedAccount = null;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                materializedAccount = ResolveMaterializedMicrosoftAccountCore(candidateIdentifiers);
                if (materializedAccount is null)
                {
                    throw new InvalidOperationException(
                        "Microsoft sign-in completed, but Emerald could not materialize the signed-in account.");
                }

                if (GetSelectedAccountCore() is null)
                    ApplySelectedAccountCore(materializedAccount.UniqueId, persist: true);
            }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        _logger.LogInformation(
            "Microsoft account '{Name}' materialized with identifier '{Identifier}'.",
            materializedAccount!.Name,
            materializedAccount.UniqueId);
    }

    public async Task SignInElyByAccountAsync()
    {
        EnsureElyByAccountPolicyMet("Signing in with Ely.by requires at least one Microsoft account.");

        var state = CreateOAuthState();
        var authorizationRequest = _elyByAuthClient.CreateOAuthAuthorizationRequest(state);

        _logger.LogInformation("Starting Ely.by browser sign-in.");
        var authorizationResult = await _elyByOAuthBrowser
            .AuthorizeAsync(authorizationRequest)
            .ConfigureAwait(false);
        var session = await _elyByAuthClient
            .ExchangeOAuthCodeAsync(authorizationResult.Code)
            .ConfigureAwait(false);

        await AddOrUpdateElyBySessionAsync(session).ConfigureAwait(false);
        _logger.LogInformation("Ely.by account '{Name}' signed in through OAuth.", session.Name);
    }

    public async Task SignInElyByAccountAsync(string login, string password, string? twoFactorCode = null)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Ely.by username or email cannot be empty.", nameof(login));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Ely.by password cannot be empty.", nameof(password));

        EnsureElyByAccountPolicyMet("Signing in with Ely.by requires at least one Microsoft account.");

        _logger.LogInformation("Starting Ely.by sign-in for '{Login}'.", login);
        var session = await _elyByAuthClient
            .AuthenticateAsync(login.Trim(), password, string.IsNullOrWhiteSpace(twoFactorCode) ? null : twoFactorCode.Trim())
            .ConfigureAwait(false);

        await AddOrUpdateElyBySessionAsync(session).ConfigureAwait(false);
        _logger.LogInformation("Ely.by account '{Name}' signed in.", session.Name);
    }

    private async Task AddOrUpdateElyBySessionAsync(ElyByAuthSession session)
    {
        var storedAccount = CreateStoredElyByAccount(session);
        _elyByAccountStore.Upsert(storedAccount);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                var account = _accounts.FirstOrDefault(candidate =>
                    candidate.Type == AccountType.ElyBy &&
                    string.Equals(candidate.UniqueId, storedAccount.UniqueId, StringComparison.Ordinal));

                if (account is null)
                {
                    account = CreateElyByAccount(storedAccount);
                    _accounts.Add(account);
                }
                else
                {
                    account.Name = storedAccount.Name;
                    account.UUID = storedAccount.UUID;
                    account.LastUsed = storedAccount.LastUsed;
                    EnsureProviderId(account);
                }

                if (GetSelectedAccountCore() is null)
                    ApplySelectedAccountCore(account.UniqueId, persist: false);
            }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        PersistAccounts();
    }

    private static string CreateOAuthState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IReadOnlyList<string> BuildMaterializationCandidates(
        MicrosoftInteractiveSignInResult signInResult,
        ISet<string> beforeIdentifiers,
        IReadOnlyList<MicrosoftAccountInfo> afterAccounts,
        string? defaultAccountIdentifier)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        static void AddCandidate(List<string> candidates, HashSet<string> seen, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !seen.Add(identifier))
                return;

            candidates.Add(identifier);
        }

        AddCandidate(candidates, seen, signInResult.Identifier);
        AddCandidate(candidates, seen, signInResult.UUID);

        foreach (var addedAccount in afterAccounts
                     .Where(account => !beforeIdentifiers.Contains(account.Identifier))
                     .OrderByDescending(account => account.LastAccess))
        {
            AddCandidate(candidates, seen, addedAccount.Identifier);
        }

        AddCandidate(candidates, seen, defaultAccountIdentifier);
        AddCandidate(candidates, seen, afterAccounts.OrderByDescending(account => account.LastAccess).FirstOrDefault()?.Identifier);

        return candidates;
    }

    private EAccount? ResolveMaterializedMicrosoftAccountCore(IEnumerable<string> candidateIdentifiers)
    {
        foreach (var identifier in candidateIdentifiers)
        {
            var matched = _accounts.FirstOrDefault(account =>
                account.Type == AccountType.Microsoft &&
                string.Equals(account.UniqueId, identifier, StringComparison.Ordinal));

            if (matched is not null)
                return matched;
        }

        return null;
    }
}
