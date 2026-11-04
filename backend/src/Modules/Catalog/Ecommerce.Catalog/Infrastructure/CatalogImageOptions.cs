namespace Ecommerce.Catalog.Infrastructure;

internal sealed class CatalogImageOptions
{
    /// <summary>
    /// Public base URL for product images (Spaces/CDN domain is an open decision, architecture 2).
    /// Empty by design in committed config: image URLs are then null.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    public static bool IsValid(CatalogImageOptions o) =>
        string.IsNullOrWhiteSpace(o.BaseUrl)
        || (Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
