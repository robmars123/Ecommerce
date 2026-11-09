using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Catalog.Domain;
using Ecommerce.Persistence.Projections;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Projections;

// Tests 12 and 17 (arch 11.2) and the best-seller sort and related products (Slice 6b D15, D16).
public class CatalogSalesTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> SlugOfAsync(ProjectionEnv e, Guid variant) =>
        await e.F.ScalarAsync<string>("SELECT p.slug FROM products p JOIN product_variants v ON v.product_id = p.id WHERE v.id = @v", ("v", variant));

    /// <summary>A published product with one variant in an existing category; returns the variant id.</summary>
    private static async Task<Guid> AddInCategoryAsync(ProjectionEnv e, Guid categoryId, string key, bool publish = true, DateTimeOffset? at = null)
    {
        DateTimeOffset now = at ?? e.Env.Clock.UtcNow;
        Product product = Product.Create(Guid.CreateVersion7(now), "p-" + key.ToLowerInvariant(), "Product " + key, "", categoryId, now);
        ProductVariant variant = product.AddVariant(Guid.CreateVersion7(now), key, key, null, null, 10m, "USD", null, 100, now);
        if (publish)
        {
            product.Publish(now);
        }
        e.Env.Db.Add(product);
        await e.Env.Db.SaveChangesAsync(Ct);
        await InventoryEnvSetStockAsync(e, variant.Id);
        return variant.Id;
    }

    private static Task InventoryEnvSetStockAsync(ProjectionEnv e, Guid variant) => Inventory.InventoryEnv.SetStockAsync(e.Env, variant, 50);

    private static async Task PayAsync(ProjectionEnv e, Guid variant, int quantity)
    {
        Guid order = await e.F.PlaceOrderAsync(variant, quantity, "cs-" + Guid.NewGuid().ToString("N"));
        await e.F.PayAsync(order, "evt_cs_" + Guid.NewGuid().ToString("N"));
    }

    private static async Task<string[]> BestSellingSlugsAsync(ProjectionEnv e)
    {
        IdentityTestEnv.Api r = await e.Env.GetAsync("/api/v1/products?sort=bestselling");
        Assert.Equal(HttpStatusCode.OK, r.Status);
        return [.. r.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!)];
    }

    [Fact]
    public async Task T12_the_same_OrderPaid_delivered_twice_adds_the_units_once_and_leaves_the_best_seller_order_unchanged()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid sold = await e.F.Cart.VariantAsync("BS-SOLD", stock: 50);
        Guid unsold = await e.F.Cart.VariantAsync("BS-UNSOLD", stock: 50);
        await PayAsync(e, sold, 3);
        await e.DrainAsync();
        string[] before = await BestSellingSlugsAsync(e);

        await e.DrainAsync();
        using (IServiceScope scope = e.Env.Factory.Services.CreateScope())
        {
            Guid eventId = await e.F.ScalarAsync<Guid>("SELECT event_id FROM outbox_events WHERE event_type = 'OrderPaid'");
            OutboxMessage message = new(
                eventId, "OrderPaid", "Order", await e.F.ScalarAsync<Guid>("SELECT aggregate_id FROM outbox_events WHERE event_id = @i", ("i", eventId)), 1,
                await e.F.ScalarAsync<string>("SELECT payload::text FROM outbox_events WHERE event_id = @i", ("i", eventId)), e.Env.Clock.UtcNow);
            IProjector projector = scope.ServiceProvider.GetServices<IProjector>().Single(p => p.Name == "catalog.search");
            Assert.False(await scope.ServiceProvider.GetRequiredService<ProjectionProcessor>().ApplyAsync(projector, message, 1, Ct));
        }

        Assert.Equal(3, await e.F.ScalarAsync<int>("SELECT units FROM catalog_product_sales_daily"));
        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM catalog_product_sales_daily"));
        Assert.Equal(before, await BestSellingSlugsAsync(e));
        Assert.Equal([await SlugOfAsync(e, sold), await SlugOfAsync(e, unsold)], before);
    }

    [Fact]
    public async Task The_bestselling_sort_puts_more_units_first_and_unsold_products_last()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid one = await e.F.Cart.VariantAsync("BS-ONE", stock: 50);
        Guid three = await e.F.Cart.VariantAsync("BS-THREE", stock: 50);
        Guid none = await e.F.Cart.VariantAsync("BS-NONE", stock: 50);
        await PayAsync(e, one, 1);
        await PayAsync(e, three, 3);
        await e.DrainAsync();

        Assert.Equal([await SlugOfAsync(e, three), await SlugOfAsync(e, one), await SlugOfAsync(e, none)], await BestSellingSlugsAsync(e));
    }

    [Fact]
    public async Task A_product_detail_has_at_most_four_related_products_of_its_category_best_sellers_first_never_itself_or_a_hidden_one()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid first = await e.F.Cart.VariantAsync("REL-0", stock: 50);
        Guid category = await e.F.ScalarAsync<Guid>("SELECT category_id FROM products p JOIN product_variants v ON v.product_id = p.id WHERE v.id = @v", ("v", first));
        List<Guid> siblings = [];
        for (int i = 1; i <= 5; i++)
        {
            siblings.Add(await AddInCategoryAsync(e, category, "REL-" + i));
        }
        await AddInCategoryAsync(e, category, "REL-DRAFT", publish: false);
        await e.F.Cart.VariantAsync("REL-OTHER-CATEGORY", stock: 50);
        await PayAsync(e, siblings[2], 4);
        await PayAsync(e, siblings[4], 2);
        await e.DrainAsync();

        IdentityTestEnv.Api r = await e.Env.GetAsync("/api/v1/products/" + await SlugOfAsync(e, first));

        Assert.Equal(HttpStatusCode.OK, r.Status);
        string[] related = [.. r.Json.GetProperty("related").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!)];
        Assert.Equal(4, related.Length);
        Assert.Equal([await SlugOfAsync(e, siblings[2]), await SlugOfAsync(e, siblings[4])], related.Take(2));
        Assert.DoesNotContain(await SlugOfAsync(e, first), related);
        Assert.DoesNotContain("p-rel-draft", related);
        Assert.DoesNotContain("p-rel-other-category", related);
        Assert.True(r.Json.GetProperty("related")[0].TryGetProperty("price", out _), "related items use the list-item shape");
    }

    [Fact]
    public async Task T17_replaying_both_projectors_from_the_retained_outbox_gives_identical_bestsellers_related_top_products_and_KPIs()
    {
        await using ProjectionEnv e = await ProjectionEnv.StartAsync(postgres);
        Guid a = await e.F.Cart.VariantAsync("RP-A", stock: 50);
        Guid category = await e.F.ScalarAsync<Guid>("SELECT category_id FROM products p JOIN product_variants v ON v.product_id = p.id WHERE v.id = @v", ("v", a));
        Guid b = await AddInCategoryAsync(e, category, "RP-B");
        Guid c = await AddInCategoryAsync(e, category, "RP-C");
        await PayAsync(e, a, 1);
        await PayAsync(e, b, 3);
        await PayAsync(e, b, 1);
        Guid refunded = await e.F.PlaceOrderAsync(c, 2, "rp-" + Guid.NewGuid().ToString("N"));
        await e.F.PayAsync(refunded, "evt_rp_refunded");
        await e.R.RefundWebhookAsync(RefundIdOf(await e.R.RefundAsync(refunded, "3.00")), "succeeded");
        await e.DrainAsync();
        (string list, string related, string top, string kpis, string statuses) = await SnapshotAsync(e, a);
        Assert.NotEqual("", list);

        foreach (string name in new[] { "catalog.search", "admin.dashboard" })
        {
            Assert.Equal(HttpStatusCode.Accepted, (await e.Env.PostAsync($"/api/v1/admin/projections/{name}/replay", null, e.R.Admin)).Status);
        }
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM catalog_product_sales_daily"));
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM admin_dashboard_orders"));
        await e.DrainAsync();

        Assert.Equal((list, related, top, kpis, statuses), await SnapshotAsync(e, a));
        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM admin_dashboard_refunds"));
    }

    private static Guid RefundIdOf(IdentityTestEnv.Api accepted) => accepted.Json.GetProperty("refundId").GetGuid();

    private static async Task<(string List, string Related, string Top, string Kpis, string Statuses)> SnapshotAsync(ProjectionEnv e, Guid variant)
    {
        IdentityTestEnv.Api list = await e.Env.GetAsync("/api/v1/products?sort=bestselling");
        IdentityTestEnv.Api detail = await e.Env.GetAsync("/api/v1/products/" + await SlugOfAsync(e, variant));
        IdentityTestEnv.Api dashboard = await e.OkDashboardAsync();
        return (
            list.Json.GetProperty("items").GetRawText(),
            detail.Json.GetProperty("related").GetRawText(),
            dashboard.Json.GetProperty("topProducts").GetRawText(),
            dashboard.Json.GetProperty("kpis").GetRawText(),
            dashboard.Json.GetProperty("ordersByStatus").GetRawText());
    }
}
