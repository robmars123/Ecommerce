using Ecommerce.Inventory.Contracts;
using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Application;

internal sealed class CatalogBrowseService(
    ICatalogReadStore store,
    IInventoryReader inventory,
    IImageUrlResolver images)
{
    private const int RelatedCount = 4;
    private const int RelatedCandidates = 20;

    public async Task<ProductListPage> SearchAsync(ProductSearchCriteria criteria, CancellationToken ct)
    {
        ProductPageRows rows = await store.SearchAsync(criteria, ct);
        IReadOnlyDictionary<Guid, StockStatus> statuses = await StatusesAsync(rows.Items.SelectMany(r => r.ActiveVariantIds), ct);

        List<ProductListItem> items = rows.Items.Select(row => ToItem(row, statuses)).ToList();

        int totalPages = (int)Math.Ceiling(rows.TotalCount / (double)criteria.PageSize);
        return new ProductListPage(items, criteria.Page, criteria.PageSize, rows.TotalCount, totalPages);
    }

    public async Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        if (!Slug.IsValid(slug))
        {
            return null;
        }

        ProductDetailRow? row = await store.GetVisibleBySlugAsync(slug, ct);
        if (row is null)
        {
            return null;
        }

        // Related (Slice 6b D16): up to four other visible products of the same category, best sellers first, then newest.
        // One stock lookup serves the page's variants and the related cards together (one inventory call per request).
        ProductPageRows candidates = await store.SearchAsync(new ProductSearchCriteria(null, row.Category.Slug, ProductSort.BestSelling, 1, RelatedCandidates), ct);
        List<ProductListRow> relatedRows = candidates.Items.Where(i => i.Id != row.Id && i.Category.Slug == row.Category.Slug).Take(RelatedCount).ToList();

        IReadOnlyDictionary<Guid, StockStatus> statuses = await StatusesAsync(
            row.Variants.Select(v => v.Id).Concat(relatedRows.SelectMany(r => r.ActiveVariantIds)), ct);
        return new ProductDetail(
            row.Id, row.Slug, row.Name, row.Description, row.Category,
            row.Images.Select(i => new ImageView(images.Resolve(i.StorageKey), i.AltText)).ToList(),
            row.Variants.Select(v => new VariantView(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency,
                statuses.GetValueOrDefault(v.Id, StockStatus.Out))).ToList(),
            relatedRows.Select(r => ToItem(r, statuses)).ToList());
    }

    private async Task<IReadOnlyDictionary<Guid, StockStatus>> StatusesAsync(IEnumerable<Guid> variantIds, CancellationToken ct)
    {
        List<Guid> ids = variantIds.Distinct().ToList();
        return ids.Count == 0 ? new Dictionary<Guid, StockStatus>() : await inventory.GetStockStatusesAsync(ids, ct);
    }

    private ProductListItem ToItem(ProductListRow row, IReadOnlyDictionary<Guid, StockStatus> statuses) =>
        new(row.Id, row.Slug, row.Name, row.Category, row.Price, row.CompareAtPrice, row.Currency,
            row.PrimaryImageKey is null ? null : new ImageView(images.Resolve(row.PrimaryImageKey), row.PrimaryImageAlt ?? ""),
            StockStatusRollup.Rollup(row.ActiveVariantIds.Select(id => statuses.GetValueOrDefault(id, StockStatus.Out))));

    public async Task<IReadOnlyList<CategoryRow>> GetCategoriesAsync(CancellationToken ct) =>
        await store.ListCategoriesAsync(ct);
}
