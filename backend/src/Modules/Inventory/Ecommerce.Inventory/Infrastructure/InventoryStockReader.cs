using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Inventory.Infrastructure;

/// <summary>
/// Display-only stock classification (arch 7.4). One entry per distinct requested ID; a variant with no stock row is
/// Out. Never returns Unknown. One no-tracking query.
/// </summary>
internal sealed class InventoryStockReader(IInventoryStore store, StockSettings settings) : IInventoryReader
{
    public async Task<IReadOnlyDictionary<Guid, StockStatus>> GetStockStatusesAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        List<Guid> ids = variantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, StockStatus>();
        }

        IReadOnlyDictionary<Guid, StockCounts> counts = await store.GetAsync(ids, ct);
        int threshold = settings.LowStockThreshold;
        return ids.ToDictionary(
            id => id,
            id => counts.TryGetValue(id, out StockCounts? c) ? StockLevel.Classify(c.Available, threshold) : StockStatus.Out);
    }

    public async Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        List<Guid> ids = variantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, StockAvailability>();
        }

        IReadOnlyDictionary<Guid, StockCounts> counts = await store.GetAsync(ids, ct);
        return ids.ToDictionary(id => id, id =>
        {
            int available = counts.TryGetValue(id, out StockCounts? c) ? Math.Max(0, c.Available) : 0;
            return new StockAvailability(available, StockLevel.Classify(available, settings.LowStockThreshold));
        });
    }

    public async Task<IReadOnlyList<LowStockItem>> ListLowStockAsync(int max, CancellationToken ct) =>
        await store.ListLowAsync(settings.LowStockThreshold, max, ct);

    public async Task<int> CountLowStockAsync(CancellationToken ct) =>
        await store.CountLowAsync(settings.LowStockThreshold, ct);
}
