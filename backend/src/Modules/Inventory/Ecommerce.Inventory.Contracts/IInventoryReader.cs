namespace Ecommerce.Inventory.Contracts;

public sealed record LowStockItem(Guid VariantId, int Available);

/// <summary>Display-grade availability: never below 0. A variant with no stock row is 0 / Out.</summary>
public sealed record StockAvailability(int Available, StockStatus Status);

/// <summary>
/// Inventory module public read contract. Display-only (architecture 8.1): no locks, never used by checkout.
/// <see cref="GetStockStatusesAsync"/> hides quantities (list and detail pages); <see cref="GetAvailabilityAsync"/>
/// returns the available quantity for the cart's 409 and merge caps.
/// </summary>
public interface IInventoryReader
{
    Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct);

    /// <summary>One entry per distinct requested ID.</summary>
    Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct);

    /// <summary>Stock rows with available at or below the low-stock threshold (0 included), lowest first, then variant id. Display-only, no locks.</summary>
    Task<IReadOnlyList<LowStockItem>> ListLowStockAsync(int max, CancellationToken ct);
}
