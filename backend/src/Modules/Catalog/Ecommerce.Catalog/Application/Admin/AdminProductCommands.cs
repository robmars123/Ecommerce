namespace Ecommerce.Catalog.Application.Admin;

/// <summary>
/// An existing variant carries its <see cref="Id"/>; a new one has none. SKU and currency of an existing variant
/// are immutable. Money arrives already parsed from the exact string the admin typed; nothing is computed here.
/// </summary>
internal sealed record VariantInput(
    Guid? Id,
    string Sku,
    string Name,
    string? Size,
    string? Colour,
    decimal Price,
    string Currency,
    decimal? CompareAtPrice,
    int WeightGrams,
    bool IsActive);

internal sealed record ImageInput(string StorageKey, string AltText);

internal sealed record CreateProductCommand(
    string Slug,
    string Name,
    string Description,
    string CategorySlug,
    IReadOnlyList<VariantInput> Variants,
    IReadOnlyList<ImageInput> Images);

internal sealed record UpdateProductCommand(
    uint Version,
    string Slug,
    string Name,
    string Description,
    string CategorySlug,
    IReadOnlyList<VariantInput> Variants,
    IReadOnlyList<ImageInput> Images);

internal sealed record CreateCategoryCommand(string Slug, string Name, string? ParentSlug);

internal sealed record UpdateCategoryCommand(string CurrentSlug, string Slug, string Name);

internal sealed record UploadImageCommand(Stream Content, long Length);
