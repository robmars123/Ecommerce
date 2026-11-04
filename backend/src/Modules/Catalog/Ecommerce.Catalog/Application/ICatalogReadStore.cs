namespace Ecommerce.Catalog.Application;

/// <summary>Catalog module-internal read port. Slice 6b swaps the source behind it.</summary>
internal interface ICatalogReadStore
{
    Task<ProductPageRows> SearchAsync(ProductSearchCriteria criteria, CancellationToken ct);

    Task<ProductDetailRow?> GetVisibleBySlugAsync(string slug, CancellationToken ct);

    Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken ct);
}
