using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Domain;
using Ecommerce.Inventory.Infrastructure;

namespace Ecommerce.Inventory.Tests.Application;

// Test 20 (unit half): one entry per distinct requested ID, never Unknown.
public class InventoryStockReaderTests
{
    private sealed class FakeStore(Dictionary<Guid, StockCounts> counts) : IInventoryStore
    {
    public Task<IReadOnlyList<LowStockItem>> ListLowAsync(int threshold, int max, CancellationToken ct) => throw new NotSupportedException();
    public Task<int> CountLowAsync(int threshold, CancellationToken ct) => throw new NotSupportedException();

        public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

        public Task<IReadOnlyDictionary<Guid, StockCounts>> GetAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
        {
            Calls.Add(variantIds);
            return Task.FromResult<IReadOnlyDictionary<Guid, StockCounts>>(counts);
        }

        public Task EnsureRowAsync(Guid variantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<InventoryStock>> LockAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> FindExpiredOrderIdsAsync(DateTimeOffset now, int max, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<InventoryReservation>> LockActiveReservationsAsync(Guid orderId, CancellationToken ct) => throw new NotSupportedException();
        public void AddReservation(InventoryReservation reservation) => throw new NotSupportedException();
    }

    private static InventoryStockReader Reader(FakeStore store) =>
        new(store, new StockSettings(5));

    [Fact]
    public async Task Returns_one_entry_per_distinct_id_with_one_store_call()
    {
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        FakeStore store = new(new Dictionary<Guid, StockCounts> { [a] = new StockCounts(9, 0) });

        IReadOnlyDictionary<Guid, StockStatus> result = await Reader(store).GetStockStatusesAsync([a, a, b, b, b], CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(StockStatus.InStock, result[a]);
        Assert.Equal(StockStatus.Out, result[b]);
        Assert.Equal(2, Assert.Single(store.Calls).Count);
    }

    [Fact]
    public async Task No_ids_returns_an_empty_map_without_touching_the_store()
    {
        FakeStore store = new([]);

        IReadOnlyDictionary<Guid, StockStatus> result = await Reader(store).GetStockStatusesAsync([], CancellationToken.None);

        Assert.Empty(result);
        Assert.Empty(store.Calls);
    }
}
