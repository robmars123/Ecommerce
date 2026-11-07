namespace Ecommerce.Cart.Domain;

internal sealed class CartItem
{
    private CartItem() { }

    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public int Quantity { get; private set; }

    /// <summary>The price last shown to the customer. Used only to report PriceChanged, never for a total.</summary>
    public decimal DisplayUnitPrice { get; private set; }
    public string Currency { get; private set; } = "";
    public DateTimeOffset AddedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static CartItem Create(Guid cartId, Guid variantId, int quantity, decimal price, string currency, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now), CartId = cartId, ProductVariantId = variantId, Quantity = quantity,
            DisplayUnitPrice = price, Currency = currency, AddedAt = now, UpdatedAt = now,
        };

    internal void SetQuantity(int quantity, decimal price, string currency, DateTimeOffset now)
    {
        Quantity = quantity;
        DisplayUnitPrice = price;
        Currency = currency;
        UpdatedAt = now;
    }
}
