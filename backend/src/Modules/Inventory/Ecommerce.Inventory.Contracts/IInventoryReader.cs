namespace Ecommerce.Inventory.Contracts;

/// <summary>
/// Inventory module public read contract. Display-only (architecture 8.1). Checkout never uses this.
/// Inventory classifies stock itself (its own low-stock threshold), so callers never see quantities.
/// </summary>
public interface IInventoryReader
{
    Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct);
}
