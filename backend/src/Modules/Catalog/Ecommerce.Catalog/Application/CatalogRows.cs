namespace Ecommerce.Catalog.Application;

internal sealed record CategoryRef(string Slug, string Name);

internal sealed record CategoryRow(string Slug, string Name, string? ParentSlug);

internal sealed record ProductListRow(
    Guid Id,
    string Slug,
    string Name,
    CategoryRef Category,
    decimal Price,
    decimal? CompareAtPrice,
    string Currency,
    string? PrimaryImageKey,
    string? PrimaryImageAlt,
    IReadOnlyList<Guid> ActiveVariantIds);

internal sealed record ProductPageRows(IReadOnlyList<ProductListRow> Items, int TotalCount);

internal sealed record VariantRow(
    Guid Id,
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    decimal Price,
    decimal? CompareAtPrice,
    string Currency);

internal sealed record ImageRow(string StorageKey, string AltText);

internal sealed record ProductDetailRow(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    CategoryRef Category,
    IReadOnlyList<VariantRow> Variants,
    IReadOnlyList<ImageRow> Images);
