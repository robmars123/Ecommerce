namespace Ecommerce.Catalog.Domain;

internal sealed class ProductImage
{
    private ProductImage() { }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string StorageKey { get; private set; } = "";
    public string AltText { get; private set; } = "";
    public int SortOrder { get; private set; }

    internal static ProductImage Create(Guid id, Guid productId, string storageKey, string altText, int sortOrder) =>
        new() { Id = id, ProductId = productId, StorageKey = storageKey, AltText = altText, SortOrder = sortOrder };
}
