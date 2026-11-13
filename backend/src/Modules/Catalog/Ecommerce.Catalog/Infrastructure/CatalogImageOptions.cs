namespace Ecommerce.Catalog.Infrastructure;

internal sealed class CatalogImageOptions
{
    /// <summary>
    /// Optional public base URL for product images (a CDN or the API host; the domain is an open decision, architecture 2).
    /// Empty by design in committed config: image URLs are then the API's own /api/v1/images/{key} path.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    public static bool IsValid(CatalogImageOptions o) =>
        string.IsNullOrWhiteSpace(o.BaseUrl)
        || (Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
