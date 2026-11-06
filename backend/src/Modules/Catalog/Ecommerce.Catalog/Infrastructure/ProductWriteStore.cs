using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ecommerce.Catalog.Infrastructure;

internal sealed class ProductWriteStore(EcommerceDbContext db) : IProductWriteStore
{
    public Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        db.Set<Product>()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Add(Product product) => db.Set<Product>().Add(product);

    public void SetExpectedVersion(Product product, uint version)
    {
        EntityEntry<Product> entry = db.Entry(product);
        // The UPDATE becomes ... WHERE id = @id AND xmin = @version, so a stale editor gets a concurrency conflict.
        entry.Property(p => p.Version).OriginalValue = version;
        // Force the product row into the UPDATE on every admin save. Without this, an edit that only touches variants or
        // images (or a frozen clock that leaves updated_at unchanged) would skip the version check entirely.
        entry.Property(p => p.UpdatedAt).IsModified = true;
    }

    public void RemoveImages(IEnumerable<ProductImage> images) => db.Set<ProductImage>().RemoveRange(images);

    public Task<Guid?> FindCategoryIdBySlugAsync(string slug, CancellationToken ct) =>
        db.Set<Category>().AsNoTracking()
            .Where(c => c.Slug == slug)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
}

internal sealed class CategoryWriteStore(EcommerceDbContext db) : ICategoryWriteStore
{
    private sealed record ParentRef(Guid Id, Guid? ParentId);

    public async Task<(Guid Id, Guid? ParentId)?> FindBySlugAsync(string slug, CancellationToken ct)
    {
        ParentRef? row = await db.Set<Category>().AsNoTracking()
            .Where(c => c.Slug == slug)
            .Select(c => new ParentRef(c.Id, c.ParentId))
            .FirstOrDefaultAsync(ct);
        return row is null ? null : (row.Id, row.ParentId);
    }

    public void Add(Category category) => db.Set<Category>().Add(category);
}
