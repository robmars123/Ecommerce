namespace Ecommerce.Catalog.Application.Admin;

internal sealed record AdminVariantRow(
    Guid Id,
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    decimal Price,
    string Currency,
    decimal? CompareAtPrice,
    int WeightGrams,
    bool IsActive);

internal sealed record AdminImageRow(string StorageKey, string AltText, int SortOrder);

internal sealed record AdminProductRow(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    CategoryRef Category,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminVariantRow> Variants,
    IReadOnlyList<AdminImageRow> Images);

internal sealed record AdminProductListRow(
    Guid Id,
    string Slug,
    string Name,
    CategoryRef Category,
    bool IsActive,
    int VariantCount,
    int ActiveVariantCount,
    decimal? Price,
    string? Currency,
    DateTimeOffset UpdatedAt);

internal sealed record AdminProductPageRows(IReadOnlyList<AdminProductListRow> Items, int TotalCount);

internal sealed record AdminPreviewRow(ProductDetailRow Product, bool IsActive);
