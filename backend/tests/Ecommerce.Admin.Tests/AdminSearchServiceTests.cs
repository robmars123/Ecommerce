using Ecommerce.Admin.Application;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Orders.Contracts;

namespace Ecommerce.Admin.Tests;

// Slice P2.8 U4-U6: the role branch (Support never reaches Catalog), the limit, and no reshaping.
public class AdminSearchServiceTests
{
    private sealed class FakeOrders(IReadOnlyList<OrderSearchHit> hits) : IOrderSearch
    {
        public List<(string Term, int Max)> Calls { get; } = [];

        public Task<IReadOnlyList<OrderSearchHit>> SearchByNumberAsync(string term, int max, CancellationToken ct)
        {
            Calls.Add((term, max));
            return Task.FromResult(hits);
        }
    }

    private sealed class FakeCatalog(IReadOnlyList<ProductSearchHit> products, IReadOnlyList<SkuSearchHit> skus) : ICatalogSearch
    {
        public List<(string Term, int Max)> ProductCalls { get; } = [];
        public List<(string Term, int Max)> SkuCalls { get; } = [];

        public Task<IReadOnlyList<ProductSearchHit>> SearchProductsAsync(string term, int max, CancellationToken ct)
        {
            ProductCalls.Add((term, max));
            return Task.FromResult(products);
        }

        public Task<IReadOnlyList<SkuSearchHit>> SearchSkusAsync(string term, int max, CancellationToken ct)
        {
            SkuCalls.Add((term, max));
            return Task.FromResult(skus);
        }
    }

    private static readonly IReadOnlyList<OrderSearchHit> OrderHits =
        [new OrderSearchHit(Guid.NewGuid(), "ORD-2", "Paid"), new OrderSearchHit(Guid.NewGuid(), "ORD-1", "Shipped")];
    private static readonly IReadOnlyList<ProductSearchHit> ProductHits = [new ProductSearchHit(Guid.NewGuid(), "Zeta"), new ProductSearchHit(Guid.NewGuid(), "Alpha")];
    private static readonly IReadOnlyList<SkuSearchHit> SkuHits = [new SkuSearchHit("Z-1", Guid.NewGuid(), "Zeta"), new SkuSearchHit("A-1", Guid.NewGuid(), "Alpha")];

    [Fact]
    public async Task U4_Without_catalog_access_products_and_skus_are_empty_and_catalog_is_never_called()
    {
        FakeOrders orders = new FakeOrders(OrderHits);
        FakeCatalog catalog = new FakeCatalog(ProductHits, SkuHits);

        AdminSearchView view = await new AdminSearchService(orders, catalog).SearchAsync("ab", false, TestContext.Current.CancellationToken);

        Assert.Empty(view.Products);
        Assert.Empty(view.Skus);
        Assert.Empty(catalog.ProductCalls);
        Assert.Empty(catalog.SkuCalls);
        Assert.Equal(2, view.Orders.Count);
    }

    [Fact]
    public async Task U5_With_catalog_access_every_source_is_called_once_with_the_term_and_max_5()
    {
        FakeOrders orders = new FakeOrders(OrderHits);
        FakeCatalog catalog = new FakeCatalog(ProductHits, SkuHits);

        await new AdminSearchService(orders, catalog).SearchAsync("mug", true, TestContext.Current.CancellationToken);

        Assert.Equal([("mug", 5)], orders.Calls);
        Assert.Equal([("mug", 5)], catalog.ProductCalls);
        Assert.Equal([("mug", 5)], catalog.SkuCalls);
    }

    [Fact]
    public async Task U6_Lists_come_back_unchanged()
    {
        AdminSearchView view = await new AdminSearchService(new FakeOrders(OrderHits), new FakeCatalog(ProductHits, SkuHits))
            .SearchAsync("mug", true, TestContext.Current.CancellationToken);

        Assert.Equal(OrderHits, view.Orders);
        Assert.Equal(ProductHits, view.Products);
        Assert.Equal(SkuHits, view.Skus);
    }
}
