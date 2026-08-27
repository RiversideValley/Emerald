using Emerald.CoreX.Models;
using Emerald.CoreX.Services.Auth;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    public async Task<EAccount> SignInAsync(string providerId, AccountSignInRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(request);
        await EnsureInitializedAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!_providers.TryGetValue(providerId, out var provider))
            throw new ArgumentException($"Unknown account provider: {providerId}", nameof(providerId));

        var method = provider.Descriptor.SignInMethods.FirstOrDefault(candidate =>
            string.Equals(candidate.MethodId, request.MethodId, StringComparison.Ordinal));
        if (method is null)
            throw new ArgumentException(
                $"Provider '{providerId}' does not expose sign-in method '{request.MethodId}'.",
                nameof(request));
        if (method.InputKind == AccountSignInInputKind.Username && string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException($"Sign-in method '{request.MethodId}' requires a username.", nameof(request));

        _uiDispatcher.Invoke(() => EnsureProviderUsableCore(providerId));

        var account = await provider.SignInAsync(request, cancellationToken).ConfigureAwait(false);
        account.ProviderId = provider.Descriptor.ProviderId;
        account.ProviderDisplayName = provider.Descriptor.DisplayName;
        account.ProviderActions = provider.Descriptor.EffectiveActions;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                var existing = _accounts.FirstOrDefault(candidate =>
                    string.Equals(candidate.ProviderId, account.ProviderId, StringComparison.Ordinal) &&
                    string.Equals(candidate.UniqueId, account.UniqueId, StringComparison.Ordinal));
                if (existing is null)
                {
                    _accounts.Add(account);
                }
                else
                {
                    existing.Name = account.Name;
                    existing.UUID = account.UUID;
                    existing.LastUsed = account.LastUsed;
                    existing.Availability = account.Availability;
                    existing.AvailabilityMessage = account.AvailabilityMessage;
                    existing.ProviderActions = account.ProviderActions;
                    account = existing;
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
        return account;
    }
}
