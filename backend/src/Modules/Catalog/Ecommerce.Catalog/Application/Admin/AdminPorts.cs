using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Application.Admin;

/// <summary>
/// Write side of the admin catalog. Optimistic concurrency, not a lock: an admin edit spans human think time,
/// so a stale <c>version</c> is detected at save instead of holding a row lock (D12).
/// </summary>
internal interface IProductWriteStore
{
    /// <summary>Tracked, with variants and images.</summary>
    Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct);

    void Add(Product product);

    /// <summary>
    /// Makes the next save fail with a concurrency conflict unless the row still has <paramref name="version"/>.
    /// The row is forced into the UPDATE even when only children changed, so the check always runs.
    /// </summary>
    void SetExpectedVersion(Product product, uint version);

    void RemoveImages(IEnumerable<ProductImage> images);

    Task<Guid?> FindCategoryIdBySlugAsync(string slug, CancellationToken ct);
}

internal interface ICategoryWriteStore
{
    Task<(Guid Id, Guid? ParentId)?> FindBySlugAsync(string slug, CancellationToken ct);

    void Add(Category category);

    Task<Category?> GetForUpdateAsync(string slug, CancellationToken ct);

    Task<string?> FindSlugByIdAsync(Guid id, CancellationToken ct);

    void Remove(Category category);
}

internal interface IAdminCatalogReadStore
{
    Task<AdminProductPageRows> ListAsync(AdminProductListCriteria criteria, CancellationToken ct);

    Task<AdminProductRow?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>The public detail shape (active variants only) for a product regardless of is_active.</summary>
    Task<AdminPreviewRow?> GetPreviewAsync(Guid id, CancellationToken ct);
}
