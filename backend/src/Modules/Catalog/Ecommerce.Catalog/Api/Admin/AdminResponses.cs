using Ecommerce.Catalog.Application;
using Ecommerce.Catalog.Application.Admin;

namespace Ecommerce.Catalog.Api.Admin;

internal sealed record AdminVariantResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    MoneyResponse Price,
    MoneyResponse? CompareAtPrice,
    int WeightGrams,
    bool IsActive);

internal sealed record AdminImageResponse(string StorageKey, string AltText, int SortOrder, string? Url);

internal sealed record AdminProductResponse(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    CategoryRefResponse Category,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminVariantResponse> Variants,
    IReadOnlyList<AdminImageResponse> Images);

internal sealed record AdminProductListItemResponse(
    Guid Id,
    string Slug,
    string Name,
    CategoryRefResponse Category,
    bool IsActive,
    int VariantCount,
    int ActiveVariantCount,
    MoneyResponse? Price,
    DateTimeOffset UpdatedAt);

internal sealed record AdminProductListResponse(
    IReadOnlyList<AdminProductListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal sealed record AdminPreviewResponse(ProductDetailResponse Product, bool IsActive, bool IsVisibleToCustomers);

internal static class AdminResponseMapping
{
    public static AdminProductResponse ToResponse(this AdminProductView v) => new(
        v.Id, v.Slug, v.Name, v.Description, new CategoryRefResponse(v.Category.Slug, v.Category.Name),
        v.IsActive, v.Version, v.CreatedAt, v.UpdatedAt,
        v.Variants.Select(x => new AdminVariantResponse(
            x.Id, x.Sku, x.Name, x.Size, x.Colour,
            MoneyResponse.From(x.Price, x.Currency),
            x.CompareAtPrice is { } c ? MoneyResponse.From(c, x.Currency) : null,
            x.WeightGrams, x.IsActive)).ToList(),
        v.Images.Select(i => new AdminImageResponse(i.StorageKey, i.AltText, i.SortOrder, i.Url)).ToList());

    public static AdminProductListResponse ToResponse(this AdminProductPage page) => new(
        page.Items.Select(i => new AdminProductListItemResponse(
            i.Id, i.Slug, i.Name, new CategoryRefResponse(i.Category.Slug, i.Category.Name), i.IsActive,
            i.VariantCount, i.ActiveVariantCount,
            i.Price is { } price && i.Currency is { } currency ? MoneyResponse.From(price, currency) : null,
            i.UpdatedAt)).ToList(),
        page.Page, page.PageSize, page.TotalCount, page.TotalPages);

    public static AdminPreviewResponse ToResponse(this AdminPreview p) =>
        new(p.Product.ToResponse(), p.IsActive, p.IsVisibleToCustomers);

    public static CategoryResponse ToResponse(this CategoryView c) => new(c.Slug, c.Name, c.ParentSlug);
}
