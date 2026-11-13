namespace Ecommerce.Catalog.Contracts;

/// <summary>IsActive is true only when both the product and the variant are active.</summary>
public sealed record VariantRef(Guid VariantId, Guid ProductId, string ProductName, string Sku, string VariantName, bool IsActive);

public sealed record VariantRefPage(IReadOnlyList<VariantRef> Items, int TotalCount);

public sealed record CartImage(string Url, string AltText);

/// <summary>Current display data for one variant. Image is null when the product has none or no image base URL is configured. Colour is null when the variant has none.</summary>
public sealed record VariantSnapshot(
    Guid VariantId, string ProductSlug, string ProductName, string VariantName, CartImage? Image,
    decimal Price, string Currency, bool IsActive, string? Colour);

/// <summary>Catalog's public lookup for other modules (rule 8): no Catalog table is read from outside.</summary>
public interface IProductReader
{
    /// <summary>Unknown IDs are omitted.</summary>
    Task<IReadOnlyList<VariantRef>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct);

    /// <summary>Current price, currency, activity and primary image per variant. Unknown IDs are omitted.</summary>
    Task<IReadOnlyList<VariantSnapshot>> GetVariantSnapshotsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct);

    /// <summary>Every variant (active or not), ordered by product name, SKU, id. Pages are 1-based.</summary>
    Task<VariantRefPage> ListVariantsAsync(int page, int pageSize, CancellationToken ct);
}
