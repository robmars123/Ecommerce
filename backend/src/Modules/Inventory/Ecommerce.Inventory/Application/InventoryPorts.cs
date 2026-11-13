using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Domain;

namespace Ecommerce.Inventory.Application;

/// <summary>The low-stock threshold, built once from <c>Inventory:LowStockThreshold</c> (the module wires it up).</summary>
internal sealed record StockSettings(int LowStockThreshold);

internal sealed record StockCounts(int OnHand, int Reserved)
{
    public int Available => OnHand - Reserved;
}

/// <summary>
/// Inventory's own persistence port. Lock methods need an open transaction (they throw otherwise) and take row
/// locks in variant-ID order, decided in SQL (rule 3).
/// </summary>
internal interface IInventoryStore
{
    /// <summary>Creates the stock row (0 / 0) if missing. Safe when two callers race on the same variant.</summary>
    Task EnsureRowAsync(Guid variantId, CancellationToken ct);

    /// <summary>SELECT ... ORDER BY product_variant_id FOR UPDATE. Missing rows are simply absent.</summary>
    Task<IReadOnlyList<InventoryStock>> LockAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct);

    /// <summary>No-tracking read; a variant without a row is absent from the map.</summary>
    Task<IReadOnlyDictionary<Guid, StockCounts>> GetAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct);

    /// <summary>No-tracking: variants with available at or below the threshold, lowest first.</summary>
    Task<IReadOnlyList<LowStockItem>> ListLowAsync(int threshold, int max, CancellationToken ct);

    /// <summary>No-tracking: how many variants have available at or below the threshold (same rule as <see cref="ListLowAsync"/>).</summary>
    Task<int> CountLowAsync(int threshold, CancellationToken ct);

    Task<IReadOnlyList<Guid>> FindExpiredOrderIdsAsync(DateTimeOffset now, int max, CancellationToken ct);

    /// <summary>One order's Active reservations, FOR UPDATE.</summary>
    Task<IReadOnlyList<InventoryReservation>> LockActiveReservationsAsync(Guid orderId, CancellationToken ct);

    void AddReservation(InventoryReservation reservation);
}
