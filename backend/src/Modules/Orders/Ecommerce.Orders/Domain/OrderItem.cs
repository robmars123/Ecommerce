namespace Ecommerce.Orders.Domain;

/// <summary>A price/name snapshot line, written once by checkout and never edited.</summary>
internal sealed class OrderItem
{
    private OrderItem() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public string SkuSnapshot { get; private set; } = "";
    public string NameSnapshot { get; private set; } = "";
    public decimal UnitPriceSnapshot { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal { get; private set; }

    public static OrderItem Create(Guid id, Guid orderId, Guid variantId, string sku, string name, decimal unitPrice, int quantity) =>
        new()
        {
            Id = id, OrderId = orderId, ProductVariantId = variantId, SkuSnapshot = sku, NameSnapshot = name,
            UnitPriceSnapshot = unitPrice, Quantity = quantity, LineTotal = unitPrice * quantity,
        };
}
