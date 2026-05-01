using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Services.Auth.Authlib;

namespace Emerald.CoreX.Services.Auth.ElyBy;

internal sealed class ElyByAccountAuthenticationProvider(
    IElyByAccountStore accountStore,
    IElyByAuthClient authClient,
    IAuthlibInjectorService authlibInjectorService) : IAccountAuthenticationProvider
{
    public AccountType AccountType => AccountType.ElyBy;
    public string ProviderId => AccountProviderIds.ElyBy;

    public async Task<GameAuthenticationResult> AuthenticateAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        var storedAccount = accountStore.Find(account.UniqueId)
            ?? throw new InvalidOperationException($"Ely.by account '{account.Name}' is no longer signed in.");

        ElyByAuthSession session;
        if (await authClient.ValidateAsync(storedAccount.AccessToken, storedAccount.ClientToken, cancellationToken).ConfigureAwait(false))
        {
            session = new ElyByAuthSession(storedAccount.Name, storedAccount.UUID, storedAccount.AccessToken, storedAccount.ClientToken);
        }
        else
        {
            session = await authClient.RefreshAsync(storedAccount, cancellationToken).ConfigureAwait(false);
        }

        storedAccount.Name = session.Name;
        storedAccount.UUID = session.UUID;
        storedAccount.AccessToken = session.AccessToken;
        storedAccount.ClientToken = session.ClientToken;
        storedAccount.LastUsed = DateTime.UtcNow;
        accountStore.Upsert(storedAccount);

        var javaAgentArgument = await authlibInjectorService.GetJavaAgentArgumentAsync(cancellationToken).ConfigureAwait(false);
        var runtimeOptions = new AccountRuntimeAuthOptions([new MArgument(javaAgentArgument)]);

        return new GameAuthenticationResult(
            new MSession
            {
                Username = session.Name,
                UUID = session.UUID,
                AccessToken = session.AccessToken,
                ClientToken = session.ClientToken,
                UserType = "msa"
            },
            runtimeOptions);
    }

    public async Task RemoveAsync(EAccount account, CancellationToken cancellationToken = default)
    {
        var storedAccount = accountStore.Find(account.UniqueId);
        if (storedAccount is not null)
        {
            await authClient.InvalidateAsync(storedAccount, cancellationToken).ConfigureAwait(false);
        }

        accountStore.Remove(account.UniqueId);
    }
}
