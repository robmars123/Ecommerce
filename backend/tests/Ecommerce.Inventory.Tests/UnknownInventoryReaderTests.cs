using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Infrastructure;

namespace Ecommerce.Inventory.Tests;

public class UnknownInventoryReaderTests
{
    private readonly UnknownInventoryReader _reader = new();

    [Fact]
    public async Task Returns_Unknown_for_every_requested_variant()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var result = await _reader.GetStockStatusesAsync(ids, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.All(ids, id => Assert.Equal(StockStatus.Unknown, result[id]));
    }

    [Fact]
    public async Task Returns_one_entry_per_distinct_variant_id()
    {
        var id = Guid.NewGuid();

        var result = await _reader.GetStockStatusesAsync([id, id, id], CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal(id, entry.Key);
        Assert.Equal(StockStatus.Unknown, entry.Value);
    }

    [Fact]
    public async Task Returns_an_empty_map_for_no_ids()
    {
        var result = await _reader.GetStockStatusesAsync([], CancellationToken.None);

        Assert.Empty(result);
    }
}
