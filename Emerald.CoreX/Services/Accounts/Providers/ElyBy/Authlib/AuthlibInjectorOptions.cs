namespace Emerald.CoreX.Services.Auth.Authlib;

public sealed record AuthlibInjectorOptions(string RecommendedVersion)
{
    public Uri ArtifactApiRoot { get; init; } = new("https://authlib-injector.yushi.moe/");
    public Uri PrimaryMetadataEndpoint { get; init; } = new("https://ely.by");
    public Uri FallbackMetadataEndpoint { get; init; } = new("https://account.ely.by/api/authlib-injector");
    public int MaximumMetadataBytes { get; init; } = 256 * 1024;
}
