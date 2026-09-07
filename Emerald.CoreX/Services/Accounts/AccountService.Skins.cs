using System.Collections.Concurrent;
using Emerald.CoreX.Models;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService
{
    private readonly ConcurrentDictionary<string, Task<AccountSkinData>> _skinRequests = new(StringComparer.Ordinal);

    public async Task<AccountSkinData> GetSkinAsync(
        EAccount account,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await EnsureInitializedAsync().ConfigureAwait(false);
        EnsureProviderId(account);

        var key = GetSkinCacheKey(account);
        if (forceRefresh)
        {
            _skinRequests.TryRemove(key, out _);
            await _uiDispatcher.InvokeAsync(() => account.Skin = null).ConfigureAwait(false);
        }

        if (!forceRefresh && account.Skin is { } existing)
            return existing;

        var request = _skinRequests.GetOrAdd(key, _ => LoadSkinAsync(account));
        try
        {
            return await request.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _skinRequests.TryRemove(new KeyValuePair<string, Task<AccountSkinData>>(key, request));
            throw;
        }
    }

    private async Task<AccountSkinData> LoadSkinAsync(EAccount account)
    {
        AccountSkinData? skin = null;
        try
        {
            skin = await GetProvider(account).GetSkinAsync(account).ConfigureAwait(false);
            if (skin is not null && !MinecraftSkinTextures.IsSupportedSkinPng(skin.PngBytes))
            {
                _logger.LogWarning("Provider {ProviderId} returned an invalid skin texture for account {AccountId}.", account.ProviderId, account.UniqueId);
                skin = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to retrieve skin for account {AccountId} from provider {ProviderId}.", account.UniqueId, account.ProviderId);
        }

        skin ??= MinecraftSkinTextures.CreateSteveFallback(account.ProviderDisplayName);
        await _uiDispatcher.InvokeAsync(() => account.Skin = skin).ConfigureAwait(false);
        return skin;
    }

    private void InvalidateSkin(EAccount account)
    {
        _skinRequests.TryRemove(GetSkinCacheKey(account), out _);
        _uiDispatcher.Invoke(() => account.Skin = null);
    }

    private static string GetSkinCacheKey(EAccount account)
        => $"{account.ProviderId}:{account.UniqueId}";
}
