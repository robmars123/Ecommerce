using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.Inventory.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Catalog.Tests.Application;

internal sealed class AdminFakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class AdminFakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    /// <summary>Thrown by the next save, simulating a database error.</summary>
    public Exception? FailOnSave { get; set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saves++;
        return FailOnSave is null ? Task.CompletedTask : Task.FromException(FailOnSave);
    }

    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
}

internal sealed class FakeProductWriteStore : IProductWriteStore
{
    public Dictionary<Guid, Product> Products { get; } = [];
    public Dictionary<string, Guid> Categories { get; } = [];
    public List<(Product Product, uint Version)> ExpectedVersions { get; } = [];
    public List<ProductImage> RemovedImages { get; } = [];

    public Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Products.GetValueOrDefault(id));

    public void Add(Product product) => Products[product.Id] = product;

    public void SetExpectedVersion(Product product, uint version) => ExpectedVersions.Add((product, version));

    public void RemoveImages(IEnumerable<ProductImage> images) => RemovedImages.AddRange(images);

    public Task<Guid?> FindCategoryIdBySlugAsync(string slug, CancellationToken ct) =>
        Task.FromResult(Categories.TryGetValue(slug, out Guid id) ? (Guid?)id : null);
}

internal sealed class FakeAdminReadStore(FakeProductWriteStore source) : IAdminCatalogReadStore
{
    public Task<AdminProductPageRows> ListAsync(AdminProductListCriteria criteria, CancellationToken ct) =>
        Task.FromResult(new AdminProductPageRows([], 0));

    public Task<bool> ProductExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(source.Products.ContainsKey(id));

    public Task<AdminProductRow?> GetAsync(Guid id, CancellationToken ct)
    {
        if (!source.Products.TryGetValue(id, out Product? p))
        {
            return Task.FromResult<AdminProductRow?>(null);
        }
        string categorySlug = source.Categories.First(c => c.Value == p.CategoryId).Key;
        return Task.FromResult<AdminProductRow?>(new AdminProductRow(
            p.Id, p.Slug, p.Name, p.Description, new CategoryRef(categorySlug, categorySlug), p.IsActive, p.Version,
            p.CreatedAt, p.UpdatedAt,
            p.Variants.Select(v => new AdminVariantRow(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.Currency, v.CompareAtPrice, v.WeightGrams, v.IsActive)).ToList(),
            p.Images.OrderBy(i => i.SortOrder).Select(i => new AdminImageRow(i.StorageKey, i.AltText, i.SortOrder)).ToList()));
    }

    public Task<AdminPreviewRow?> GetPreviewAsync(Guid id, CancellationToken ct)
    {
        if (!source.Products.TryGetValue(id, out Product? p))
        {
            return Task.FromResult<AdminPreviewRow?>(null);
        }
        string categorySlug = source.Categories.First(c => c.Value == p.CategoryId).Key;
        ProductDetailRow row = new ProductDetailRow(
            p.Id, p.Slug, p.Name, p.Description, new CategoryRef(categorySlug, categorySlug),
            p.Variants.Where(v => v.IsActive)
                .Select(v => new VariantRow(v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency)).ToList(),
            p.Images.Select(i => new ImageRow(i.StorageKey, i.AltText)).ToList());
        return Task.FromResult<AdminPreviewRow?>(new AdminPreviewRow(row, p.IsActive));
    }
}

internal sealed class CountingInventory : IInventoryReader
{
    public Task<IReadOnlyList<LowStockItem>> ListLowStockAsync(int max, CancellationToken ct) => throw new NotSupportedException();

    public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

    public Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        Calls.Add(variantIds);
        return Task.FromResult<IReadOnlyDictionary<Guid, StockStatus>>(variantIds.ToDictionary(id => id, _ => StockStatus.Out));
    }

    public Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) =>
        throw new NotSupportedException();
}

internal sealed class NoImages : IImageUrlResolver
{
    public string? Resolve(string storageKey) => null;
}

internal sealed class AdminEnv
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public AdminEnv()
    {
        Writes = new FakeProductWriteStore();
        Writes.Categories["tops"] = Guid.NewGuid();
        Writes.Categories["shoes"] = Guid.NewGuid();
        Reads = new FakeAdminReadStore(Writes);
        Inventory = new CountingInventory();
        UnitOfWork = new AdminFakeUnitOfWork();
        Clock = new AdminFakeClock(T0);
        Service = new AdminProductService(Writes, Reads, Inventory, new NoImages(), UnitOfWork, Clock);
    }

    public FakeProductWriteStore Writes { get; }
    public FakeAdminReadStore Reads { get; }
    public CountingInventory Inventory { get; }
    public AdminFakeUnitOfWork UnitOfWork { get; }
    public AdminFakeClock Clock { get; }
    public AdminProductService Service { get; }

    public static VariantInput Variant(string sku = "LS-M", Guid? id = null, decimal price = 49m, string currency = "USD", bool active = true) =>
        new(id, sku, "Medium", "M", null, price, currency, null, 250, active);

    public static CreateProductCommand CreateCommand(string slug = "linen-shirt", string category = "tops", params VariantInput[] variants) =>
        new(slug, "Linen Shirt", "A shirt", category, variants.Length == 0 ? [Variant()] : variants, [new ImageInput("products/x/1.jpg", "Front")]);

    /// <summary>A product that exists in the fake store (inactive, one active variant "LS-M").</summary>
    public Product SeedProduct(bool published = false, params string[] skus)
    {
        Product product = Product.Create(Guid.NewGuid(), "linen-shirt", "Linen Shirt", "A shirt", Writes.Categories["tops"], T0);
        foreach (string sku in skus.Length == 0 ? ["LS-M"] : skus)
        {
            product.AddVariant(Guid.NewGuid(), sku, "Medium", "M", null, 49m, "USD", null, 250, T0);
        }
        if (published)
        {
            product.Publish(T0);
        }
        Writes.Add(product);
        return product;
    }

    public static UpdateProductCommand UpdateCommand(Product product, uint version = 7, IEnumerable<VariantInput>? variants = null,
        string? slug = null, string category = "tops") =>
        new(version, slug ?? product.Slug, product.Name, product.Description, category,
            (variants ?? product.Variants.Select(v => Variant(v.Sku, v.Id, v.Price, v.Currency, v.IsActive))).ToList(),
            [new ImageInput("products/x/2.jpg", "Back")]);
}
