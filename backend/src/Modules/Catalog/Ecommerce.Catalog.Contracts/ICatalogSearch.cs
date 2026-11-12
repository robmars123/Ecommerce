namespace Ecommerce.Catalog.Contracts;

public sealed record ProductSearchHit(Guid Id, string Name);

public sealed record SkuSearchHit(string Sku, Guid ProductId, string ProductName);

public interface ICatalogSearch
{
    /// <summary>Active and inactive products, case-insensitive substring on the name, by name then id, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<ProductSearchHit>> SearchProductsAsync(string term, int max, CancellationToken ct);

    /// <summary>Active and inactive variants, case-insensitive substring on the SKU, by SKU then id, at most <paramref name="max"/>.</summary>
    Task<IReadOnlyList<SkuSearchHit>> SearchSkusAsync(string term, int max, CancellationToken ct);
}
