using Ecommerce.Inventory.Contracts;
using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Application;

internal sealed class CatalogBrowseService(
    ICatalogReadStore store,
    IInventoryReader inventory,
    IImageUrlResolver images)
{
    public async Task<ProductListPage> SearchAsync(ProductSearchCriteria criteria, CancellationToken ct)
    {
        ProductPageRows rows = await store.SearchAsync(criteria, ct);

        List<Guid> allIds = rows.Items.SelectMany(r => r.ActiveVariantIds).Distinct().ToList();
        IReadOnlyDictionary<Guid, StockStatus> statuses = allIds.Count == 0
            ? new Dictionary<Guid, StockStatus>()
            : await inventory.GetStockStatusesAsync(allIds, ct);

        List<ProductListItem> items = rows.Items.Select(row => new ProductListItem(
            row.Id, row.Slug, row.Name, row.Category, row.Price, row.CompareAtPrice, row.Currency,
            row.PrimaryImageKey is null ? null : new ImageView(images.Resolve(row.PrimaryImageKey), row.PrimaryImageAlt ?? ""),
            StockStatusRollup.Rollup(row.ActiveVariantIds.Select(id => statuses.GetValueOrDefault(id, StockStatus.Out)))))
            .ToList();

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

        IReadOnlyDictionary<Guid, StockStatus> statuses = await inventory.GetStockStatusesAsync(row.Variants.Select(v => v.Id).ToList(), ct);
        return new ProductDetail(
            row.Id, row.Slug, row.Name, row.Description, row.Category,
            row.Images.Select(i => new ImageView(images.Resolve(i.StorageKey), i.AltText)).ToList(),
            row.Variants.Select(v => new VariantView(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, v.Price, v.CompareAtPrice, v.Currency,
                statuses.GetValueOrDefault(v.Id, StockStatus.Out))).ToList());
    }

    public async Task<IReadOnlyList<CategoryRow>> GetCategoriesAsync(CancellationToken ct) =>
        await store.ListCategoriesAsync(ct);
}
