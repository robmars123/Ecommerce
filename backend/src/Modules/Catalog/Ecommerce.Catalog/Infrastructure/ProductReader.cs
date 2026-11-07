using System.Linq.Expressions;
using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Catalog.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure;

/// <summary>Catalog's public lookup (rule 8). Reads Catalog's own tables only, no tracking.</summary>
internal sealed class ProductReader(EcommerceDbContext db, IImageUrlResolver images) : IProductReader
{
    public async Task<IReadOnlyList<VariantRef>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        if (variantIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = variantIds.ToArray();
        return await Ordered(v => ids.Contains(v.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VariantSnapshot>> GetVariantSnapshotsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        if (variantIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = variantIds.Distinct().ToArray();
        List<SnapshotRow> rows = await (
            from v in db.Set<ProductVariant>().AsNoTracking().Where(v => ids.Contains(v.Id))
            join p in db.Set<Product>().AsNoTracking() on v.ProductId equals p.Id
            select new SnapshotRow(v.Id, p.Id, p.Slug, p.Name, v.Name, v.Price, v.Currency, p.IsActive && v.IsActive)).ToListAsync(ct);

        Guid[] productIds = rows.Select(r => r.ProductId).Distinct().ToArray();
        List<ProductImage> candidates = await db.Set<ProductImage>().AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId)).ToListAsync(ct);
        Dictionary<Guid, ProductImage> primary = candidates
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).First());

        return rows.Select(r =>
        {
            string? url = primary.TryGetValue(r.ProductId, out ProductImage? image) ? images.Resolve(image.StorageKey) : null;
            CartImage? cartImage = url is null ? null : new CartImage(url, primary[r.ProductId].AltText);
            return new VariantSnapshot(r.VariantId, r.Slug, r.ProductName, r.VariantName, cartImage, r.Price, r.Currency, r.IsActive);
        }).ToList();
    }

    private sealed record SnapshotRow(
        Guid VariantId, Guid ProductId, string Slug, string ProductName, string VariantName, decimal Price, string Currency, bool IsActive);

    public async Task<VariantRefPage> ListVariantsAsync(int page, int pageSize, CancellationToken ct)
    {
        int total = await db.Set<ProductVariant>().CountAsync(ct);
        List<VariantRef> items = await Ordered(v => true).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new VariantRefPage(items, total);
    }

    // Filter and order run on the entities; only then does the query project (EF cannot filter through a constructor projection).
    private IQueryable<VariantRef> Ordered(Expression<Func<ProductVariant, bool>> filter) =>
        from v in db.Set<ProductVariant>().AsNoTracking().Where(filter)
        join p in db.Set<Product>().AsNoTracking() on v.ProductId equals p.Id
        orderby p.Name, v.Sku, v.Id
        select new VariantRef(v.Id, p.Id, p.Name, v.Sku, v.Name, p.IsActive && v.IsActive);
}
