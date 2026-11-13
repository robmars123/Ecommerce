using Ecommerce.Catalog.Application;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Catalog.Tests.Application;

public class CatalogBrowseServiceTests
{
    private static readonly CategoryRef Tops = new("tops", "Tops");

    private static ProductListRow Row(string slug, params Guid[] variantIds) =>
        new(Guid.NewGuid(), slug, slug, Tops, 10m, null, "USD", null, null, variantIds);

    private static ProductSearchCriteria Criteria(int page = 1, int pageSize = 10) =>
        new(null, null, ProductSort.Newest, page, pageSize);

    private static (CatalogBrowseService Service, FakeStore Store, FakeInventory Inventory) Build(
        ProductPageRows? page = null, ProductDetailRow? detail = null, Dictionary<Guid, StockStatus>? statuses = null)
    {
        FakeStore store = new FakeStore { Page = page ?? new ProductPageRows([], 0), Detail = detail };
        FakeInventory inventory = new FakeInventory(statuses ?? []);
        return (new CatalogBrowseService(store, inventory, new FakeImages()), store, inventory);
    }

    [Fact]
    public async Task Search_calls_inventory_exactly_once_with_the_union_of_the_pages_variant_ids()
    {
        (Guid a1, Guid a2, Guid b1, Guid c1) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        ProductPageRows page = new ProductPageRows([Row("a", a1, a2), Row("b", b1), Row("c", c1)], 3);
        (CatalogBrowseService? service, FakeStore _, FakeInventory? inventory) = Build(page);

        await service.SearchAsync(Criteria(), TestContext.Current.CancellationToken);

        IReadOnlyCollection<Guid> call = Assert.Single(inventory.Calls);
        Assert.Equal(new[] { a1, a2, b1, c1 }.Order(), call.Order());
    }

    [Fact]
    public async Task Search_does_not_call_inventory_on_an_empty_page()
    {
        (CatalogBrowseService? service, FakeStore _, FakeInventory? inventory) = Build(new ProductPageRows([], 25));

        ProductListPage result = await service.SearchAsync(Criteria(page: 9), TestContext.Current.CancellationToken);

        Assert.Empty(inventory.Calls);
        Assert.Empty(result.Items);
        Assert.Equal(25, result.TotalCount);
    }

    [Fact]
    public async Task Search_maps_missing_ids_to_Out_and_rolls_up_per_product()
    {
        (Guid inStock, Guid low, Guid out1, Guid out2, Guid missing) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        ProductPageRows page = new ProductPageRows(
            [Row("mixed", inStock, out1), Row("low", low, missing), Row("out", out2), Row("unknown", missing)], 4);
        Dictionary<Guid, StockStatus> statuses = new Dictionary<Guid, StockStatus>
        {
            [inStock] = StockStatus.InStock, [low] = StockStatus.Low, [out1] = StockStatus.Out, [out2] = StockStatus.Out,
        };
        (CatalogBrowseService? service, FakeStore _, FakeInventory _) = Build(page, statuses: statuses);

        ProductListPage result = await service.SearchAsync(Criteria(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [StockStatus.InStock, StockStatus.Low, StockStatus.Out, StockStatus.Out],
            result.Items.Select(i => i.StockStatus).ToArray());
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(20, 10, 2)]
    [InlineData(25, 10, 3)]
    [InlineData(1, 10, 1)]
    public async Task Search_computes_total_pages(int total, int pageSize, int expectedPages)
    {
        (CatalogBrowseService? service, FakeStore _, FakeInventory _) = Build(new ProductPageRows([], total));

        ProductListPage result = await service.SearchAsync(Criteria(pageSize: pageSize), TestContext.Current.CancellationToken);

        Assert.Equal(expectedPages, result.TotalPages);
        Assert.Equal(total, result.TotalCount);
        Assert.Equal(pageSize, result.PageSize);
    }

    [Fact]
    public async Task Search_resolves_primary_image_url_through_the_resolver()
    {
        ProductListRow row = Row("a", Guid.NewGuid()) with { PrimaryImageKey = "k/1.jpg", PrimaryImageAlt = "front" };
        (CatalogBrowseService? service, FakeStore _, FakeInventory _) = Build(new ProductPageRows([row], 1));

        ProductListItem item = Assert.Single((await service.SearchAsync(Criteria(), TestContext.Current.CancellationToken)).Items);

        Assert.Equal(new ImageView("resolved:k/1.jpg", "front"), item.PrimaryImage);
    }

    [Theory]
    [InlineData("Blue-Shirt")]
    [InlineData("")]
    [InlineData("a b")]
    public async Task Detail_with_a_malformed_slug_returns_null_and_never_calls_the_store(string slug)
    {
        (CatalogBrowseService? service, FakeStore? store, FakeInventory? inventory) = Build();

        ProductDetail? result = await service.GetBySlugAsync(slug, TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, store.DetailCalls);
        Assert.Empty(inventory.Calls);
    }

    [Fact]
    public async Task Detail_returns_null_when_the_store_has_no_visible_product()
    {
        (CatalogBrowseService? service, FakeStore? store, FakeInventory? inventory) = Build();

        Assert.Null(await service.GetBySlugAsync("blue-shirt", TestContext.Current.CancellationToken));
        Assert.Equal(1, store.DetailCalls);
        Assert.Empty(inventory.Calls);
    }

    [Fact]
    public async Task Detail_maps_per_variant_status_with_one_inventory_call()
    {
        (Guid v1, Guid v2) = (Guid.NewGuid(), Guid.NewGuid());
        ProductDetailRow detail = new ProductDetailRow(
            Guid.NewGuid(), "blue-shirt", "Blue Shirt", "desc", Tops,
            [new VariantRow(v1, "BS-S", "Small", "S", null, 10m, null, "USD"),
             new VariantRow(v2, "BS-M", "Medium", "M", null, 12m, 15m, "USD")],
            [new ImageRow("k/1.jpg", "front"), new ImageRow("k/2.jpg", "back")]);
        Dictionary<Guid, StockStatus> statuses = new Dictionary<Guid, StockStatus> { [v1] = StockStatus.Out };
        (CatalogBrowseService? service, FakeStore _, FakeInventory? inventory) = Build(detail: detail, statuses: statuses);

        ProductDetail? result = await service.GetBySlugAsync("blue-shirt", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal([StockStatus.Out, StockStatus.Out], result.Variants.Select(v => v.StockStatus).ToArray());
        Assert.Equal(15m, result.Variants[1].CompareAtPrice);
        Assert.Equal(new string?[] { "resolved:k/1.jpg", "resolved:k/2.jpg" }, result.Images.Select(i => i.Url).ToArray());
        Assert.Single(inventory.Calls);
    }

    private sealed class FakeStore : ICatalogReadStore
    {
        public ProductPageRows Page { get; set; } = new([], 0);
        public ProductDetailRow? Detail { get; set; }
        public int DetailCalls { get; private set; }

        public Task<ProductPageRows> SearchAsync(ProductSearchCriteria criteria, CancellationToken ct) => Task.FromResult(Page);

        public Task<ProductDetailRow?> GetVisibleBySlugAsync(string slug, CancellationToken ct)
        {
            DetailCalls++;
            return Task.FromResult(Detail);
        }

        public Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CategoryRow>>([]);
    }

    private sealed class FakeInventory(Dictionary<Guid, StockStatus> statuses) : IInventoryReader
    {
        public Task<IReadOnlyList<LowStockItem>> ListLowStockAsync(int max, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountLowStockAsync(CancellationToken ct) => throw new NotSupportedException();

        public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

        public Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(
            IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
        {
            Calls.Add(variantIds);
            Dictionary<Guid, StockStatus> result = statuses.Where(kv => variantIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            return Task.FromResult<IReadOnlyDictionary<Guid, StockStatus>>(result);
        }

        public Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(
            IReadOnlyCollection<Guid> variantIds, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeImages : IImageUrlResolver
    {
        public string Resolve(string storageKey) => $"resolved:{storageKey}";
    }
}
