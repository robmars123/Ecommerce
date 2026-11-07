using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Inventory.Domain;

/// <summary>One row per variant. Every method refuses a change the table's CHECK constraint would reject (arch 7.1).</summary>
internal sealed class InventoryStock
{
    private InventoryStock() { }

    public Guid ProductVariantId { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public uint Version { get; private set; }

    public int Available => QuantityOnHand - QuantityReserved;

    public static InventoryStock Create(Guid variantId) => new() { ProductVariantId = variantId };

    public void Reserve(int quantity)
    {
        if (quantity <= 0 || quantity > Available)
        {
            throw new DomainException("Cannot reserve more than is available.");
        }
        QuantityReserved += quantity;
    }

    public void Release(int quantity)
    {
        if (quantity <= 0 || quantity > QuantityReserved)
        {
            throw new DomainException("Cannot release more than is reserved.");
        }
        QuantityReserved -= quantity;
    }

    /// <summary>Paid: the reserved units leave the shelf. on_hand and reserved fall together, so available is unchanged.</summary>
    public void Commit(int quantity)
    {
        if (quantity <= 0 || quantity > QuantityReserved)
        {
            throw new DomainException("Cannot commit more than is reserved.");
        }
        QuantityOnHand -= quantity;
        QuantityReserved -= quantity;
    }

    public void Adjust(int delta)
    {
        if (QuantityOnHand + delta < QuantityReserved)
        {
            throw new DomainException("On hand cannot fall below what is reserved.");
        }
        QuantityOnHand += delta;
    }
}
