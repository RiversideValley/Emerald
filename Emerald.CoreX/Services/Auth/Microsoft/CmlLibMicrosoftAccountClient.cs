using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Emerald.CoreX.Services;
using Microsoft.Extensions.Logging;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;

namespace Emerald.CoreX.Services.Auth.Microsoft;

internal sealed class CmlLibMicrosoftAccountClient(ILogger<AccountService> logger) : IMicrosoftAccountClient
{
    private readonly ILogger<AccountService> _logger = logger;
    private JELoginHandler? _loginHandler;

    public async Task InitializeAsync(string clientId, string accountStorePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountStorePath);

        var directory = Path.GetDirectoryName(accountStorePath)
            ?? throw new InvalidOperationException("The CmlLib account store path must include a directory.");

        Directory.CreateDirectory(directory);

        var app = await MsalClientHelper.BuildApplicationWithCache(clientId).ConfigureAwait(false);
        _loginHandler = new JELoginHandlerBuilder()
            .WithLogger(_logger)
            .WithOAuthProvider(new MsalCodeFlowProvider(app))
            .WithAccountManager(accountStorePath)
            .Build();
    }

    public IReadOnlyList<MicrosoftAccountInfo> GetAccounts()
    {
        EnsureInitialized();

        return _loginHandler!.AccountManager
            .GetAccounts()
            .OfType<JEGameAccount>()
            .Where(account => !string.IsNullOrWhiteSpace(account.Identifier))
            .Select(account => new MicrosoftAccountInfo(
                account.Identifier,
                account.Profile?.Username ?? "Microsoft Account",
                account.Profile?.UUID ?? account.Identifier,
                account.LastAccess))
            .ToList();
    }

    public string? GetDefaultAccountIdentifier()
    {
        EnsureInitialized();

        return _loginHandler!.AccountManager.GetDefaultAccount() is JEGameAccount account
            ? account.Identifier
            : null;
    }

    public async Task<MicrosoftInteractiveSignInResult> SignInInteractivelyAsync()
    {
        EnsureInitialized();

        var session = await _loginHandler!.AuthenticateInteractively().ConfigureAwait(false);
        SaveAccounts();

        return new MicrosoftInteractiveSignInResult(
            Normalize(session.UUID),
            Normalize(session.Username),
            Normalize(session.UUID));
    }

    public async Task<MSession> AuthenticateAsync(string accountIdentifier)
    {
        var account = FindAccount(accountIdentifier)
            ?? throw new InvalidOperationException($"Microsoft account '{accountIdentifier}' was not found in the CmlLib account manager.");

        var session = await _loginHandler!.Authenticate(account).ConfigureAwait(false);
        SaveAccounts();
        return session;
    }

    public async Task SignOutAsync(string accountIdentifier)
    {
        var account = FindAccount(accountIdentifier);
        if (account is null)
        {
            _logger.LogWarning("Microsoft account '{Identifier}' was not found during sign-out.", accountIdentifier);
            return;
        }

        await _loginHandler!.Signout(account).ConfigureAwait(false);
        SaveAccounts();
    }

    private JEGameAccount? FindAccount(string accountIdentifier)
    {
        EnsureInitialized();

        return _loginHandler!.AccountManager
            .GetAccounts()
            .OfType<JEGameAccount>()
            .FirstOrDefault(account => string.Equals(account.Identifier, accountIdentifier, StringComparison.Ordinal));
    }

    private void SaveAccounts()
    {
        EnsureInitialized();
        _loginHandler!.AccountManager.SaveAccounts();
    }

    private void EnsureInitialized()
    {
        if (_loginHandler is null)
            throw new InvalidOperationException("Microsoft account client was not initialized.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
