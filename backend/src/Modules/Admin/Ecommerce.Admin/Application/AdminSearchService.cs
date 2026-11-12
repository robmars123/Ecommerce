using Ecommerce.Catalog.Contracts;
using Ecommerce.Orders.Contracts;

namespace Ecommerce.Admin.Application;

internal sealed record AdminSearchView(IReadOnlyList<OrderSearchHit> Orders, IReadOnlyList<ProductSearchHit> Products, IReadOnlyList<SkuSearchHit> Skus);

/// <summary>Staff search. Support never sees catalog results (app context 2): Catalog is not even called for them.</summary>
internal sealed class AdminSearchService(IOrderSearch orders, ICatalogSearch catalog)
{
    public const int MaxPerGroup = 5;

    // One scoped DbContext sits behind these reads, so they run one after another (no Task.WhenAll).
    public async Task<AdminSearchView> SearchAsync(string term, bool includeCatalog, CancellationToken ct)
    {
        IReadOnlyList<OrderSearchHit> found = await orders.SearchByNumberAsync(term, MaxPerGroup, ct);
        if (!includeCatalog)
        {
            return new AdminSearchView(found, [], []);
        }

        IReadOnlyList<ProductSearchHit> products = await catalog.SearchProductsAsync(term, MaxPerGroup, ct);
        IReadOnlyList<SkuSearchHit> skus = await catalog.SearchSkusAsync(term, MaxPerGroup, ct);
        return new AdminSearchView(found, products, skus);
    }
}
