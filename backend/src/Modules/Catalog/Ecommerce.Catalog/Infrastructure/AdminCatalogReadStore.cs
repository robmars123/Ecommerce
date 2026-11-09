using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Ecommerce.Catalog.Infrastructure;

internal sealed class AdminCatalogReadStore(EcommerceDbContext db) : IAdminCatalogReadStore
{
    private const string SearchConfig = "english";

    private sealed record CheapestShape(decimal Price, string Currency);

    // Property-initialized (not positional) so EF can order by its members before paging.
    private sealed record AdminListShape
    {
        public required Guid Id { get; init; }
        public required string Slug { get; init; }
        public required string Name { get; init; }
        public required bool IsActive { get; init; }
        public required DateTimeOffset UpdatedAt { get; init; }
        public required string CategorySlug { get; init; }
        public required string CategoryName { get; init; }
        public required int VariantCount { get; init; }
        public required int ActiveVariantCount { get; init; }
        public required CheapestShape? Cheapest { get; init; }
    }

    private sealed record AdminVariantShape(
        Guid Id, string Sku, string Name, string? Size, string? Colour, decimal Price, string Currency,
        decimal? CompareAtPrice, int WeightGrams, bool IsActive);

    private sealed record AdminImageShape(string StorageKey, string AltText, int SortOrder);

    private sealed record AdminDetailShape(
        Guid Id, string Slug, string Name, string Description, bool IsActive, uint Version,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string CategorySlug, string CategoryName,
        List<AdminVariantShape> Variants, List<AdminImageShape> Images);

    private sealed record PreviewVariantShape(
        Guid Id, string Sku, string Name, string? Size, string? Colour, decimal Price, decimal? CompareAtPrice, string Currency);

    private sealed record PreviewImageShape(string StorageKey, string AltText);

    private sealed record AdminPreviewShape(
        Guid Id, string Slug, string Name, string Description, bool IsActive, string CategorySlug, string CategoryName,
        List<PreviewVariantShape> Variants, List<PreviewImageShape> Images);

    public async Task<AdminProductPageRows> ListAsync(AdminProductListCriteria c, CancellationToken ct)
    {
        IQueryable<Product> products = db.Set<Product>().AsNoTracking().AsQueryable();

        products = c.Status switch
        {
            AdminProductStatus.Active => products.Where(p => p.IsActive),
            AdminProductStatus.Inactive => products.Where(p => !p.IsActive),
            _ => products,
        };

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
            return new AdminProductPageRows([], totalCount);
        }

        IQueryable<AdminListShape> shaped = from p in products
                     join cat in db.Set<Category>().AsNoTracking() on p.CategoryId equals cat.Id
                     select new AdminListShape
                     {
                         Id = p.Id,
                         Slug = p.Slug,
                         Name = p.Name,
                         IsActive = p.IsActive,
                         UpdatedAt = p.UpdatedAt,
                         CategorySlug = cat.Slug,
                         CategoryName = cat.Name,
                         VariantCount = p.Variants.Count(),
                         ActiveVariantCount = p.Variants.Count(v => v.IsActive),
                         Cheapest = p.Variants.Where(v => v.IsActive)
                             .OrderBy(v => v.Price).ThenBy(v => v.Sku)
                             .Select(v => new CheapestShape(v.Price, v.Currency))
                             .FirstOrDefault(),
                     };

        List<AdminListShape> page = await shaped
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Skip((c.Page - 1) * c.PageSize)
            .Take(c.PageSize)
            .ToListAsync(ct);

        List<AdminProductListRow> items = page.Select(x => new AdminProductListRow(
            x.Id, x.Slug, x.Name, new CategoryRef(x.CategorySlug, x.CategoryName), x.IsActive,
            x.VariantCount, x.ActiveVariantCount,
            x.Cheapest?.Price, x.Cheapest?.Currency.Trim(), x.UpdatedAt)).ToList();
        return new AdminProductPageRows(items, totalCount);
    }

    public async Task<AdminProductRow?> GetAsync(Guid id, CancellationToken ct)
    {
        AdminDetailShape? row = await (from p in db.Set<Product>().AsNoTracking()
                         where p.Id == id
                         join cat in db.Set<Category>().AsNoTracking() on p.CategoryId equals cat.Id
                         select new AdminDetailShape(
                             p.Id,
                             p.Slug,
                             p.Name,
                             p.Description,
                             p.IsActive,
                             p.Version,
                             p.CreatedAt,
                             p.UpdatedAt,
                             cat.Slug,
                             cat.Name,
                             p.Variants.OrderBy(v => v.Sku)
                                 .Select(v => new AdminVariantShape(
                                     v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.Currency,
                                     v.CompareAtPrice, v.WeightGrams, v.IsActive)).ToList(),
                             p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                                 .Select(i => new AdminImageShape(i.StorageKey, i.AltText, i.SortOrder)).ToList())).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        return new AdminProductRow(
            row.Id, row.Slug, row.Name, row.Description, new CategoryRef(row.CategorySlug, row.CategoryName),
            row.IsActive, row.Version, row.CreatedAt, row.UpdatedAt,
            row.Variants.Select(v => new AdminVariantRow(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.Currency.Trim(), v.CompareAtPrice, v.WeightGrams, v.IsActive)).ToList(),
            row.Images.Select(i => new AdminImageRow(i.StorageKey, i.AltText, i.SortOrder)).ToList());
    }

    public Task<bool> ProductExistsAsync(Guid id, CancellationToken ct) =>
        db.Set<Product>().AsNoTracking().AnyAsync(p => p.Id == id, ct);

    public async Task<AdminPreviewRow?> GetPreviewAsync(Guid id, CancellationToken ct)
    {
        AdminPreviewShape? row = await (from p in db.Set<Product>().AsNoTracking()
                         where p.Id == id
                         join cat in db.Set<Category>().AsNoTracking() on p.CategoryId equals cat.Id
                         select new AdminPreviewShape(
                             p.Id,
                             p.Slug,
                             p.Name,
                             p.Description,
                             p.IsActive,
                             cat.Slug,
                             cat.Name,
                             // The same shape and ordering as the public detail, minus the is_active gate.
                             p.Variants.Where(v => v.IsActive)
                                 .OrderBy(v => v.Price).ThenBy(v => v.Sku)
                                 .Select(v => new PreviewVariantShape(v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency))
                                 .ToList(),
                             p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                                 .Select(i => new PreviewImageShape(i.StorageKey, i.AltText))
                                 .ToList())).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        ProductDetailRow detail = new ProductDetailRow(
            row.Id, row.Slug, row.Name, row.Description, new CategoryRef(row.CategorySlug, row.CategoryName),
            row.Variants.Select(v => new VariantRow(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency.Trim())).ToList(),
            row.Images.Select(i => new ImageRow(i.StorageKey, i.AltText)).ToList());
        return new AdminPreviewRow(detail, row.IsActive);
    }
}
