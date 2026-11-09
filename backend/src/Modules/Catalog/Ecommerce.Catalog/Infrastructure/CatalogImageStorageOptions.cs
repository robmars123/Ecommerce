namespace Ecommerce.Catalog.Infrastructure;

/// <summary>
/// S3-compatible storage settings (bucket, endpoint and CDN are open decisions, architecture 2): all blank in committed
/// config, secrets only from the environment at deploy. All blank or all set; a half-configured store fails startup.
/// </summary>
internal sealed class CatalogImageStorageOptions
{
    public string ServiceUrl { get; set; } = "";
    public string Region { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";

    private string[] All => [ServiceUrl, Region, Bucket, AccessKeyId, SecretAccessKey];

    public bool IsConfigured => All.All(v => !string.IsNullOrWhiteSpace(v));

    public static bool IsValid(CatalogImageStorageOptions o) =>
        o.All.All(string.IsNullOrWhiteSpace)
        || (o.IsConfigured && Uri.TryCreate(o.ServiceUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps);
}
