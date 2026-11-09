namespace Ecommerce.Catalog.Application.Admin;

internal sealed record AdminImageView(string StorageKey, string AltText, int SortOrder, string? Url);

internal sealed record AdminProductView(
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
    IReadOnlyList<AdminImageView> Images);

internal sealed record AdminProductPage(
    IReadOnlyList<AdminProductListRow> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

internal sealed record AdminPreview(ProductDetail Product, bool IsActive, bool IsVisibleToCustomers);

internal sealed record CategoryView(string Slug, string Name, string? ParentSlug);

internal sealed record UploadedImageView(string StorageKey);

/// <summary>Ok | NotFound | Invalid (field to messages) | Conflict (optional field plus a message) | Unavailable.</summary>
internal abstract record AdminResult<T>
{
    private AdminResult() { }

    public sealed record Ok(T Value) : AdminResult<T>;

    public sealed record NotFound : AdminResult<T>;

    public sealed record Invalid(IReadOnlyDictionary<string, string[]> Errors) : AdminResult<T>
    {
        public static Invalid For(string field, string message) =>
            new(new Dictionary<string, string[]> { [field] = [message] });
    }

    public sealed record Conflict(string? Field, string Message) : AdminResult<T>;

    public sealed record Unavailable(string Message) : AdminResult<T>;
}
