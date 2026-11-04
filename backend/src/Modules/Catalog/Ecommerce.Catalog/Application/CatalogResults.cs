using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Catalog.Application;

internal sealed record ImageView(string? Url, string AltText);

internal sealed record ProductListItem(
    Guid Id,
    string Slug,
    string Name,
    CategoryRef Category,
    decimal Price,
    decimal? CompareAtPrice,
    string Currency,
    ImageView? PrimaryImage,
    StockStatus StockStatus);

internal sealed record ProductListPage(
    IReadOnlyList<ProductListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal sealed record VariantView(
    Guid Id,
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    decimal Price,
    decimal? CompareAtPrice,
    string Currency,
    StockStatus StockStatus);

internal sealed record ProductDetail(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    CategoryRef Category,
    IReadOnlyList<ImageView> Images,
    IReadOnlyList<VariantView> Variants);
