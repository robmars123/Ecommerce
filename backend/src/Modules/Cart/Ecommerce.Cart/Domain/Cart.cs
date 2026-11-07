using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Cart.Domain;

/// <summary>One cart per user. A line quantity is always above 0 (the table CHECK); removing is a separate call.</summary>
internal sealed class Cart
{
    private readonly List<CartItem> _items = [];

    private Cart() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<CartItem> Items => _items;

    public static Cart Create(Guid id, Guid userId, DateTimeOffset now) => new() { Id = id, UserId = userId, UpdatedAt = now };

    public int QuantityOf(Guid variantId) => _items.FirstOrDefault(i => i.ProductVariantId == variantId)?.Quantity ?? 0;

    /// <summary>Adds to the line (creating it if absent) and refreshes its display price.</summary>
    public void Add(Guid variantId, int quantity, decimal price, string currency, DateTimeOffset now) =>
        Write(variantId, QuantityOf(variantId) + RequirePositive(quantity), price, currency, now);

    /// <summary>Sets the line to the quantity, creating it if absent, and refreshes its display price.</summary>
    public void Set(Guid variantId, int quantity, decimal price, string currency, DateTimeOffset now) =>
        Write(variantId, RequirePositive(quantity), price, currency, now);

    /// <summary>Returns false when the line was not in the cart.</summary>
    public bool Remove(Guid variantId, DateTimeOffset now)
    {
        CartItem? item = _items.FirstOrDefault(i => i.ProductVariantId == variantId);
        if (item is null)
        {
            return false;
        }
        _items.Remove(item);
        UpdatedAt = now;
        return true;
    }

    private void Write(Guid variantId, int quantity, decimal price, string currency, DateTimeOffset now)
    {
        CartItem? item = _items.FirstOrDefault(i => i.ProductVariantId == variantId);
        if (item is null)
        {
            _items.Add(CartItem.Create(Id, variantId, quantity, price, currency, now));
        }
        else
        {
            item.SetQuantity(quantity, price, currency, now);
        }
        UpdatedAt = now;
    }

    private static int RequirePositive(int quantity) =>
        quantity > 0 ? quantity : throw new DomainException("A cart line quantity must be greater than zero.");
}
