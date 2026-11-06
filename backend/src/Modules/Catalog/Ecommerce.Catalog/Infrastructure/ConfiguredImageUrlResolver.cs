using Ecommerce.Catalog.Application;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Infrastructure;

internal sealed class ConfiguredImageUrlResolver(IOptions<CatalogImageOptions> options) : IImageUrlResolver
{
    private readonly string _baseUrl = options.Value.BaseUrl?.Trim().TrimEnd('/') ?? "";

    public string? Resolve(string storageKey)
    {
        if (_baseUrl.Length == 0)
        {
            return null;
        }

        IEnumerable<string> segments = storageKey
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);
        return $"{_baseUrl}/{string.Join('/', segments)}";
    }
}
