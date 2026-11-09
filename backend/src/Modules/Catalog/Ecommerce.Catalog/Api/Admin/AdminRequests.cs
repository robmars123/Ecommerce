namespace Ecommerce.Catalog.Api.Admin;

// Explicit write DTOs (rule 1): no client-settable isActive on create, no id on a product, no computed price. Unknown JSON
// members are a 400 (strict binding), so extra fields such as "isActive" or "role" can never be silently bound or dropped.

/// <summary>Amount is a string typed by the admin ("49.00"); it is parsed with the invariant culture and stored exactly.</summary>
internal sealed record MoneyRequest(string Amount, string Currency);

/// <summary>The fields a create and an update variant share, so one validator covers both.</summary>
internal interface IVariantFields
{
    string Sku { get; }
    string Name { get; }
    string? Size { get; }
    string? Colour { get; }
    MoneyRequest Price { get; }
    MoneyRequest? CompareAtPrice { get; }
    int WeightGrams { get; }
}

internal sealed record CreateVariantRequest(
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    MoneyRequest Price,
    MoneyRequest? CompareAtPrice,
    int WeightGrams) : IVariantFields;

internal sealed record UpdateVariantRequest(
    Guid? Id,
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    MoneyRequest Price,
    MoneyRequest? CompareAtPrice,
    int WeightGrams,
    bool IsActive) : IVariantFields;

internal sealed record ImageRequest(string StorageKey, string AltText);

internal sealed record CreateProductRequest(
    string Slug,
    string Name,
    string Description,
    string CategorySlug,
    IReadOnlyList<CreateVariantRequest> Variants,
    IReadOnlyList<ImageRequest> Images);

internal sealed record UpdateProductRequest(
    uint Version,
    string Slug,
    string Name,
    string Description,
    string CategorySlug,
    IReadOnlyList<UpdateVariantRequest> Variants,
    IReadOnlyList<ImageRequest> Images);

internal sealed record CreateCategoryRequest(string Slug, string Name, string? ParentSlug);

internal sealed record UpdateCategoryRequest(string Slug, string Name);
