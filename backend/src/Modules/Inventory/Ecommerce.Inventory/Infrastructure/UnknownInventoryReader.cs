using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Inventory.Infrastructure;

// Slice 2 replaces this registration with the inventory_stock reader.
internal sealed class UnknownInventoryReader : IInventoryReader
{
    public Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, StockStatus> result = variantIds
            .Distinct()
            .ToDictionary(id => id, _ => StockStatus.Unknown);
        return Task.FromResult(result);
    }
}
