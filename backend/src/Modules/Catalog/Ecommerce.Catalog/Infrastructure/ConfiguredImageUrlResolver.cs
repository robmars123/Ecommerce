using Ecommerce.Catalog.Application;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Infrastructure;

internal sealed class ConfiguredImageUrlResolver(IOptions<CatalogImageOptions> options) : IImageUrlResolver
{
    private readonly string _baseUrl = options.Value.BaseUrl?.Trim().TrimEnd('/') ?? "";

    public string Resolve(string storageKey)
    {
        IEnumerable<string> segments = storageKey
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);
        string path = string.Join('/', segments);
        return _baseUrl.Length == 0 ? $"/api/v1/images/{path}" : $"{_baseUrl}/{path}";
    }
}
