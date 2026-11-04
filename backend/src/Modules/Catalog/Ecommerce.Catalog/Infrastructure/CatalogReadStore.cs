using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Domain;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Ecommerce.Catalog.Infrastructure;

internal sealed class CatalogReadStore(EcommerceDbContext db) : ICatalogReadStore
{
    private const string SearchConfig = "english";

    public async Task<ProductPageRows> SearchAsync(ProductSearchCriteria c, CancellationToken ct)
    {
        // Visible = active product with at least one active variant (D6).
        var products = db.Set<Product>().AsNoTracking()
            .Where(p => p.IsActive && p.Variants.Any(v => v.IsActive));

        if (c.CategorySlug is { } categorySlug)
        {
            // The category and its direct children (one level in v1), resolved inside the same statement.
            var categories = db.Set<Category>().AsNoTracking();
            products = products.Where(p => categories.Any(cat =>
                cat.Id == p.CategoryId
                && (cat.Slug == categorySlug
                    || categories.Any(parent => parent.Id == cat.ParentId && parent.Slug == categorySlug))));
        }

        if (c.Search is { } search)
        {
            products = products.Where(p =>
                EF.Property<NpgsqlTsVector>(p, ProductConfiguration.SearchVector)
                    .Matches(EF.Functions.WebSearchToTsQuery(SearchConfig, search)));
        }

        var totalCount = await products.CountAsync(ct);
        if (totalCount == 0 || (long)(c.Page - 1) * c.PageSize >= totalCount)
        {
            return new ProductPageRows([], totalCount);
        }

        var searchText = c.Search ?? "";
        var categoriesForJoin = db.Set<Category>().AsNoTracking();
        var shaped = from p in products
                     join cat in categoriesForJoin on p.CategoryId equals cat.Id
                     select new
                     {
                         p.Id,
                         p.Slug,
                         p.Name,
                         p.CreatedAt,
                         CategorySlug = cat.Slug,
                         CategoryName = cat.Name,
                         MinPrice = p.Variants.Where(v => v.IsActive).Min(v => v.Price),
                         Rank = EF.Property<NpgsqlTsVector>(p, ProductConfiguration.SearchVector)
                             .Rank(EF.Functions.WebSearchToTsQuery(SearchConfig, searchText)),
                         Cheapest = p.Variants.Where(v => v.IsActive)
                             .OrderBy(v => v.Price).ThenBy(v => v.Sku)
                             .Select(v => new { v.Price, v.CompareAtPrice, v.Currency })
                             .FirstOrDefault(),
                         FirstImage = p.Images
                             .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                             .Select(i => new { i.StorageKey, i.AltText })
                             .FirstOrDefault(),
                         ActiveVariantIds = p.Variants.Where(v => v.IsActive).Select(v => v.Id).ToList(),
                     };

        var ordered = c.Sort switch
        {
            ProductSort.Relevance => shaped.OrderByDescending(x => x.Rank).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
            ProductSort.PriceAsc => shaped.OrderBy(x => x.MinPrice).ThenBy(x => x.Id),
            ProductSort.PriceDesc => shaped.OrderByDescending(x => x.MinPrice).ThenBy(x => x.Id),
            _ => shaped.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
        };

        var page = await ordered
            .Skip((c.Page - 1) * c.PageSize)
            .Take(c.PageSize)
            .ToListAsync(ct);

        var items = page.Select(x => new ProductListRow(
            x.Id, x.Slug, x.Name, new CategoryRef(x.CategorySlug, x.CategoryName),
            x.Cheapest!.Price, x.Cheapest.CompareAtPrice, x.Cheapest.Currency.Trim(),
            x.FirstImage?.StorageKey, x.FirstImage?.AltText, x.ActiveVariantIds)).ToList();

        return new ProductPageRows(items, totalCount);
    }

    public async Task<ProductDetailRow?> GetVisibleBySlugAsync(string slug, CancellationToken ct)
    {
        var categories = db.Set<Category>().AsNoTracking();
        var row = await (from p in db.Set<Product>().AsNoTracking()
                         where p.Slug == slug && p.IsActive
                         join cat in categories on p.CategoryId equals cat.Id
                         select new
                         {
                             p.Id,
                             p.Slug,
                             p.Name,
                             p.Description,
                             CategorySlug = cat.Slug,
                             CategoryName = cat.Name,
                             Variants = p.Variants.Where(v => v.IsActive)
                                 .OrderBy(v => v.Price).ThenBy(v => v.Sku)
                                 .Select(v => new { v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency })
                                 .ToList(),
                             Images = p.Images
                                 .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                                 .Select(i => new { i.StorageKey, i.AltText })
                                 .ToList(),
                         }).FirstOrDefaultAsync(ct);

        if (row is null || row.Variants.Count == 0)
        {
            return null;
        }

        return new ProductDetailRow(
            row.Id, row.Slug, row.Name, row.Description, new CategoryRef(row.CategorySlug, row.CategoryName),
            row.Variants.Select(v => new VariantRow(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency.Trim())).ToList(),
            row.Images.Select(i => new ImageRow(i.StorageKey, i.AltText)).ToList());
    }

    public async Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken ct)
    {
        var categories = db.Set<Category>().AsNoTracking();
        return await (from cat in categories
                      join parent in categories on cat.ParentId equals parent.Id into parents
                      from parent in parents.DefaultIfEmpty()
                      orderby cat.Name, cat.Slug
                      select new CategoryRow(cat.Slug, cat.Name, parent == null ? null : parent.Slug))
            .ToListAsync(ct);
    }
}
