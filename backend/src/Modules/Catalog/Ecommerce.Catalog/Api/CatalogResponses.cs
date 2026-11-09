using System.Globalization;
using Ecommerce.Catalog.Application;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Catalog.Api;

// Wire shapes (architecture 9, plan 3.5). Money is a string so the client never parses a binary float (rule 1).
internal sealed record MoneyResponse(string Amount, string Currency)
{
    public static MoneyResponse From(decimal amount, string currency) =>
        new(amount.ToString("0.00##", CultureInfo.InvariantCulture), currency);
}

internal sealed record CategoryRefResponse(string Slug, string Name);

internal sealed record ImageResponse(string? Url, string AltText);

internal sealed record ProductListItemResponse(
    Guid Id,
    string Slug,
    string Name,
    CategoryRefResponse Category,
    MoneyResponse Price,
    MoneyResponse? CompareAtPrice,
    ImageResponse? PrimaryImage,
    StockStatus StockStatus);

internal sealed record ProductListResponse(
    IReadOnlyList<ProductListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal sealed record VariantResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    MoneyResponse Price,
    MoneyResponse? CompareAtPrice,
    StockStatus StockStatus);

internal sealed record ProductDetailResponse(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    CategoryRefResponse Category,
    IReadOnlyList<ImageResponse> Images,
    IReadOnlyList<VariantResponse> Variants,
    IReadOnlyList<ProductListItemResponse> Related);

internal sealed record CategoryResponse(string Slug, string Name, string? ParentSlug);

internal sealed record CategoryListResponse(IReadOnlyList<CategoryResponse> Items);

internal static class CatalogResponseMapping
{
    public static ProductListResponse ToResponse(this ProductListPage page) =>
        new(page.Items.Select(ToItem).ToList(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);

    private static ProductListItemResponse ToItem(ProductListItem i) =>
        new(
            i.Id, i.Slug, i.Name, i.Category.ToResponse(),
            MoneyResponse.From(i.Price, i.Currency),
            i.CompareAtPrice is { } c ? MoneyResponse.From(c, i.Currency) : null,
            i.PrimaryImage?.ToResponse(),
            i.StockStatus);

    public static ProductDetailResponse ToResponse(this ProductDetail d) =>
        new(
            d.Id, d.Slug, d.Name, d.Description, d.Category.ToResponse(),
            d.Images.Select(i => i.ToResponse()).ToList(),
            d.Variants.Select(v => new VariantResponse(
                v.Id, v.Sku, v.Name, v.Size, v.Colour,
                MoneyResponse.From(v.Price, v.Currency),
                v.CompareAtPrice is { } c ? MoneyResponse.From(c, v.Currency) : null,
                v.StockStatus)).ToList(),
            d.Related.Select(ToItem).ToList());


    public static CategoryListResponse ToResponse(this IReadOnlyList<CategoryRow> rows) =>
        new(rows.Select(r => new CategoryResponse(r.Slug, r.Name, r.ParentSlug)).ToList());

    private static CategoryRefResponse ToResponse(this CategoryRef c) => new(c.Slug, c.Name);

    private static ImageResponse ToResponse(this ImageView i) => new(i.Url, i.AltText);
}
