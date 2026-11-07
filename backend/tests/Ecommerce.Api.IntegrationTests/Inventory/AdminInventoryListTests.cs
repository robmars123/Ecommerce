using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;
using Ecommerce.Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Inventory;

public class AdminInventoryListTests(PostgresFixture postgres)
{
    private const string Url = "/api/v1/admin/inventory";

    private async Task<(IdentityTestEnv Env, string Admin)> StartAsync()
    {
        IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        return (env, await env.AccessTokenAsync("admin@example.test"));
    }

    [Fact]
    public async Task The_list_shows_every_variant_ordered_by_product_name_then_sku_with_quantities_and_a_missing_row_as_zero_and_Out()
    {
        (IdentityTestEnv env, string admin) = await StartAsync();
        await using IdentityTestEnv _ = env;
        Guid[] zebra = await InventoryEnv.AddProductAsync(env, "zebra", "Zebra mug", ["Z-2", "Z-1"]);
        Guid[] apple = await InventoryEnv.AddProductAsync(env, "apple", "Apple mug", ["A-1"]);
        await InventoryEnv.AddProductAsync(env, "hidden", "Hidden mug", ["H-1"], productActive: false);
        await InventoryEnv.SetStockAsync(env, apple[0], 9, 2);
        await InventoryEnv.SetStockAsync(env, zebra[1], 3);

        IdentityTestEnv.Api r = await env.GetAsync(Url, admin);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement[] items = r.Json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["A-1", "H-1", "Z-1", "Z-2"], items.Select(i => i.GetProperty("sku").GetString()!).ToArray());
        Assert.Equal(4, r.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, r.Json.GetProperty("totalPages").GetInt32());
        JsonElement a = items[0];
        Assert.Equal((9, 2, 7, "InStock"), (a.GetProperty("onHand").GetInt32(), a.GetProperty("reserved").GetInt32(), a.GetProperty("available").GetInt32(), a.GetProperty("stockStatus").GetString()));
        Assert.Equal(apple[0], a.GetProperty("variantId").GetGuid());
        JsonElement h = items[1];
        Assert.False(h.GetProperty("isActive").GetBoolean());
        Assert.Equal((0, 0, 0, "Out"), (h.GetProperty("onHand").GetInt32(), h.GetProperty("reserved").GetInt32(), h.GetProperty("available").GetInt32(), h.GetProperty("stockStatus").GetString()));
        Assert.Equal("Low", items[2].GetProperty("stockStatus").GetString());
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_bad_paging_is_400()
    {
        (IdentityTestEnv env, string admin) = await StartAsync();
        await using IdentityTestEnv _ = env;
        await InventoryEnv.AddProductAsync(env, "pg", "Pager", ["P-1", "P-2", "P-3"]);

        IdentityTestEnv.Api page2 = await env.GetAsync(Url + "?page=2&pageSize=2", admin);

        Assert.Equal(HttpStatusCode.OK, page2.Status);
        Assert.Equal(["P-3"], page2.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()!).ToArray());
        Assert.Equal((2, 2, 3, 2), (page2.Json.GetProperty("page").GetInt32(), page2.Json.GetProperty("pageSize").GetInt32(),
            page2.Json.GetProperty("totalCount").GetInt32(), page2.Json.GetProperty("totalPages").GetInt32()));
        foreach (string bad in new[] { "?page=0", "?pageSize=0", "?pageSize=101", "?page=-1" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await env.GetAsync(Url + bad, admin)).Status);
        }
    }

    [Fact]
    public async Task The_product_reader_returns_known_variants_only_with_the_active_flag_and_a_stable_order()
    {
        (IdentityTestEnv env, string _) = await StartAsync();
        await using IdentityTestEnv __ = env;
        Guid[] live = await InventoryEnv.AddProductAsync(env, "live", "Live", ["L-2", "L-1"]);
        Guid[] off = await InventoryEnv.AddProductAsync(env, "off", "Off", ["O-1"], productActive: false);
        Guid[] dead = await InventoryEnv.AddProductAsync(env, "dead", "Dead", ["D-1"], variantActive: false);
        using IServiceScope scope = env.Factory.Services.CreateScope();
        IProductReader reader = scope.ServiceProvider.GetRequiredService<IProductReader>();

        IReadOnlyList<VariantRef> found = await reader.GetVariantsAsync(
            [live[0], live[1], off[0], dead[0], Guid.NewGuid()], TestContext.Current.CancellationToken);

        Assert.Equal(["D-1", "L-1", "L-2", "O-1"], found.Select(v => v.Sku).ToArray());
        Assert.Equal([false, true, true, false], found.Select(v => v.IsActive).ToArray());
        Assert.Empty(await reader.GetVariantsAsync([], TestContext.Current.CancellationToken));
        VariantRefPage page = await reader.ListVariantsAsync(1, 2, TestContext.Current.CancellationToken);
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(["D-1", "L-1"], page.Items.Select(v => v.Sku).ToArray());
    }
}
