using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Inventory.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Inventory;

// Test 20: stock indicator thresholds (default threshold 5), no Unknown, and the Catalog list rollup.
public class StockStatusTests(PostgresFixture postgres)
{
    private static async Task<IReadOnlyDictionary<Guid, StockStatus>> ReadAsync(IdentityTestEnv env, params Guid[] ids)
    {
        using IServiceScope scope = env.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInventoryReader>().GetStockStatusesAsync(ids, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task T20_the_reader_classifies_available_against_the_threshold_and_a_missing_row_is_Out()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid[] v = await InventoryEnv.AddProductAsync(env, "t20", "T20", ["A", "B", "C", "D", "E", "F"]);
        await InventoryEnv.SetStockAsync(env, v[0], 6);
        await InventoryEnv.SetStockAsync(env, v[1], 5);
        await InventoryEnv.SetStockAsync(env, v[2], 1);
        await InventoryEnv.SetStockAsync(env, v[3], 0);
        await InventoryEnv.SetStockAsync(env, v[4], 3, 3);
        // v[5] has no row.

        IReadOnlyDictionary<Guid, StockStatus> s = await ReadAsync(env, v);

        Assert.Equal(StockStatus.InStock, s[v[0]]);
        Assert.Equal(StockStatus.Low, s[v[1]]);
        Assert.Equal(StockStatus.Low, s[v[2]]);
        Assert.Equal(StockStatus.Out, s[v[3]]);
        Assert.Equal(StockStatus.Out, s[v[4]]);
        Assert.Equal(StockStatus.Out, s[v[5]]);
        Assert.DoesNotContain(StockStatus.Unknown, s.Values);
        Assert.Equal(6, s.Count);
    }

    [Fact]
    public async Task T20_a_reservation_moves_the_indicator()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid variant = await InventoryEnv.AddVariantAsync(env, "MOVE-1");
        await InventoryEnv.SetStockAsync(env, variant, 6);
        Assert.Equal(StockStatus.InStock, (await ReadAsync(env, variant))[variant]);

        Assert.True((await InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(variant, 1)])).Succeeded);

        Assert.Equal(StockStatus.Low, (await ReadAsync(env, variant))[variant]);
    }

    [Fact]
    public async Task T20_the_catalog_list_rolls_up_per_arch_7_4_and_detail_shows_each_variant_and_neither_says_Unknown()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid[] mixed = await InventoryEnv.AddProductAsync(env, "mixed", "Mixed", ["MX-1", "MX-2"]);
        Guid[] lowOnly = await InventoryEnv.AddProductAsync(env, "lowonly", "Low only", ["LO-1"]);
        Guid[] gone = await InventoryEnv.AddProductAsync(env, "gone", "Gone", ["GN-1"]);
        await InventoryEnv.AddProductAsync(env, "norows", "No rows", ["NR-1"]);
        await InventoryEnv.SetStockAsync(env, mixed[0], 0);
        await InventoryEnv.SetStockAsync(env, mixed[1], 9);
        await InventoryEnv.SetStockAsync(env, lowOnly[0], 2);
        await InventoryEnv.SetStockAsync(env, gone[0], 0);

        IdentityTestEnv.Api list = await env.GetAsync("/api/v1/products?pageSize=50");
        IdentityTestEnv.Api detail = await env.GetAsync("/api/v1/products/p-mixed");

        Assert.Equal(HttpStatusCode.OK, list.Status);
        Dictionary<string, string> bySlug = list.Json.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("slug").GetString()!, i => i.GetProperty("stockStatus").GetString()!);
        Assert.Equal("InStock", bySlug["p-mixed"]);
        Assert.Equal("Low", bySlug["p-lowonly"]);
        Assert.Equal("Out", bySlug["p-gone"]);
        Assert.Equal("Out", bySlug["p-norows"]);
        Assert.DoesNotContain("Unknown", list.Text);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Dictionary<string, string> bySku = detail.Json.GetProperty("variants").EnumerateArray()
            .ToDictionary(x => x.GetProperty("sku").GetString()!, x => x.GetProperty("stockStatus").GetString()!);
        Assert.Equal("Out", bySku["MX-1"]);
        Assert.Equal("InStock", bySku["MX-2"]);
        Assert.DoesNotContain("Unknown", detail.Text);
    }

    [Fact]
    public async Task The_threshold_comes_from_configuration()
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres, new Dictionary<string, string> { ["Inventory:LowStockThreshold"] = "2" });
        Guid[] v = await InventoryEnv.AddProductAsync(env, "thr", "Thr", ["T-1", "T-2"]);
        await InventoryEnv.SetStockAsync(env, v[0], 2);
        await InventoryEnv.SetStockAsync(env, v[1], 3);

        IReadOnlyDictionary<Guid, StockStatus> s = await ReadAsync(env, v);

        Assert.Equal(StockStatus.Low, s[v[0]]);
        Assert.Equal(StockStatus.InStock, s[v[1]]);
    }
}
