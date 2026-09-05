using Emerald.CoreX.Models;

namespace Emerald.CoreX.Services.Auth;

internal static class AccountSkinDownload
{
    public static async Task<AccountSkinData?> DownloadAsync(
        HttpClient httpClient,
        Uri? uri,
        MinecraftSkinVariant variant,
        string source,
        CancellationToken cancellationToken)
    {
        if (uri is null || !uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return null;

        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MinecraftSkinTextures.MaxTextureBytes)
            return null;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
                break;
            if (output.Length + count > MinecraftSkinTextures.MaxTextureBytes)
                return null;
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }

        var bytes = output.ToArray();
        return MinecraftSkinTextures.IsSupportedSkinPng(bytes)
            ? new AccountSkinData(bytes, variant, source)
            : null;
    }
}
