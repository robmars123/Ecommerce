namespace Ecommerce.Catalog.Domain;

internal sealed class ProductVariant
{
    private ProductVariant() { }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Size { get; private set; }
    public string? Colour { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "";
    public decimal? CompareAtPrice { get; private set; }
    public int WeightGrams { get; private set; }
    public bool IsActive { get; private set; }

    internal static ProductVariant Create(
        Guid id, Guid productId, string sku, string name, string? size, string? colour,
        decimal price, string currency, decimal? compareAtPrice, int weightGrams) =>
        new()
        {
            Id = id, ProductId = productId, Sku = sku, Name = name, Size = size, Colour = colour,
            Price = price, Currency = currency, CompareAtPrice = compareAtPrice,
            WeightGrams = weightGrams, IsActive = true,
        };

    public void Deactivate() => IsActive = false;
}
