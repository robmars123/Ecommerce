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

    private sealed record CheapestShape(decimal Price, decimal? CompareAtPrice, string Currency);

    private sealed record ImageShape(string StorageKey, string AltText);

    // Property-initialized (not positional) so EF can order by its members before paging.
    private sealed record ListShape
    {
        public required Guid Id { get; init; }
        public required string Slug { get; init; }
        public required string Name { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required string CategorySlug { get; init; }
        public required string CategoryName { get; init; }
        public required decimal MinPrice { get; init; }
        public required float Rank { get; init; }
        public required CheapestShape? Cheapest { get; init; }
        public required ImageShape? FirstImage { get; init; }
        public required List<Guid> ActiveVariantIds { get; init; }
    }

    private sealed record VariantShape(
        Guid Id, string Sku, string Name, string? Size, string? Colour, decimal Price, decimal? CompareAtPrice, string Currency);

    private sealed record DetailShape(
        Guid Id, string Slug, string Name, string Description, string CategorySlug, string CategoryName,
        List<VariantShape> Variants, List<ImageShape> Images);

    public async Task<ProductPageRows> SearchAsync(ProductSearchCriteria c, CancellationToken ct)
    {
        // Visible = active product with at least one active variant (D6).
        IQueryable<Product> products = db.Set<Product>().AsNoTracking()
            .Where(p => p.IsActive && p.Variants.Any(v => v.IsActive));

        if (c.CategorySlug is { } categorySlug)
        {
            // The category and its direct children (one level in v1), resolved inside the same statement.
            IQueryable<Category> categories = db.Set<Category>().AsNoTracking();
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

        int totalCount = await products.CountAsync(ct);
        if (totalCount == 0 || (long)(c.Page - 1) * c.PageSize >= totalCount)
        {
            return new ProductPageRows([], totalCount);
        }

        string searchText = c.Search ?? "";
        IQueryable<Category> categoriesForJoin = db.Set<Category>().AsNoTracking();
        IQueryable<ListShape> shaped = from p in products
                     join cat in categoriesForJoin on p.CategoryId equals cat.Id
                     select new ListShape
                     {
                         Id = p.Id,
                         Slug = p.Slug,
                         Name = p.Name,
                         CreatedAt = p.CreatedAt,
                         CategorySlug = cat.Slug,
                         CategoryName = cat.Name,
                         MinPrice = p.Variants.Where(v => v.IsActive).Min(v => v.Price),
                         Rank = EF.Property<NpgsqlTsVector>(p, ProductConfiguration.SearchVector)
                             .Rank(EF.Functions.WebSearchToTsQuery(SearchConfig, searchText)),
                         Cheapest = p.Variants.Where(v => v.IsActive)
                             .OrderBy(v => v.Price).ThenBy(v => v.Sku)
                             .Select(v => new CheapestShape(v.Price, v.CompareAtPrice, v.Currency))
                             .FirstOrDefault(),
                         FirstImage = p.Images
                             .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                             .Select(i => new ImageShape(i.StorageKey, i.AltText))
                             .FirstOrDefault(),
                         ActiveVariantIds = p.Variants.Where(v => v.IsActive).Select(v => v.Id).ToList(),
                     };

        IOrderedQueryable<ListShape> ordered = c.Sort switch
        {
            ProductSort.Relevance => shaped.OrderByDescending(x => x.Rank).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
            ProductSort.PriceAsc => shaped.OrderBy(x => x.MinPrice).ThenBy(x => x.Id),
            ProductSort.PriceDesc => shaped.OrderByDescending(x => x.MinPrice).ThenBy(x => x.Id),
            _ => shaped.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id),
        };

        List<ListShape> page = await ordered
            .Skip((c.Page - 1) * c.PageSize)
            .Take(c.PageSize)
            .ToListAsync(ct);

        List<ProductListRow> items = page.Select(x => new ProductListRow(
            x.Id, x.Slug, x.Name, new CategoryRef(x.CategorySlug, x.CategoryName),
            x.Cheapest!.Price, x.Cheapest.CompareAtPrice, x.Cheapest.Currency.Trim(),
            x.FirstImage?.StorageKey, x.FirstImage?.AltText, x.ActiveVariantIds)).ToList();

        return new ProductPageRows(items, totalCount);
    }

    public async Task<ProductDetailRow?> GetVisibleBySlugAsync(string slug, CancellationToken ct)
    {
        IQueryable<Category> categories = db.Set<Category>().AsNoTracking();
        DetailShape? row = await (from p in db.Set<Product>().AsNoTracking()
                         where p.Slug == slug && p.IsActive
                         join cat in categories on p.CategoryId equals cat.Id
                         select new DetailShape(
                             p.Id,
                             p.Slug,
                             p.Name,
                             p.Description,
                             cat.Slug,
                             cat.Name,
                             p.Variants.Where(v => v.IsActive)
                                 .OrderBy(v => v.Price).ThenBy(v => v.Sku)
                                 .Select(v => new VariantShape(v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency))
                                 .ToList(),
                             p.Images
                                 .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                                 .Select(i => new ImageShape(i.StorageKey, i.AltText))
                                 .ToList())).FirstOrDefaultAsync(ct);

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
        IQueryable<Category> categories = db.Set<Category>().AsNoTracking();
        return await (from cat in categories
                      join parent in categories on cat.ParentId equals parent.Id into parents
                      from parent in parents.DefaultIfEmpty()
                      orderby cat.Name, cat.Slug
                      select new CategoryRow(cat.Slug, cat.Name, parent == null ? null : parent.Slug))
            .ToListAsync(ct);
    }
}
