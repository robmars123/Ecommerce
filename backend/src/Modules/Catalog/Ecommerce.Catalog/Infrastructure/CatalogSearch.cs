using Ecommerce.Catalog.Contracts;
using Ecommerce.Catalog.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure;

/// <summary>Admin search over products and SKUs: Catalog's own tables, read-only, active and inactive.</summary>
internal sealed class CatalogSearch(EcommerceDbContext db) : ICatalogSearch
{
    public async Task<IReadOnlyList<ProductSearchHit>> SearchProductsAsync(string term, int max, CancellationToken ct)
    {
        string pattern = Contains(term);
        return await db.Set<Product>().AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Name, pattern, "\\"))
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Take(max)
            .Select(p => new ProductSearchHit(p.Id, p.Name))
            .ToListAsync(ct);
    }

    // Filter and order run on the entities; only then does the query project (EF cannot filter through a constructor projection).
    public async Task<IReadOnlyList<SkuSearchHit>> SearchSkusAsync(string term, int max, CancellationToken ct)
    {
        string pattern = Contains(term);
        return await (
            from v in db.Set<ProductVariant>().AsNoTracking().Where(v => EF.Functions.ILike(v.Sku, pattern, "\\"))
            join p in db.Set<Product>().AsNoTracking() on v.ProductId equals p.Id
            orderby v.Sku, v.Id
            select new SkuSearchHit(v.Sku, p.Id, p.Name))
            .Take(max)
            .ToListAsync(ct);
    }

    private static string Contains(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
