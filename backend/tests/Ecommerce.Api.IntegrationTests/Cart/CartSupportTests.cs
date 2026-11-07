using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Ecommerce.Catalog.Contracts;
using Ecommerce.Inventory.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Cart;

// Schema, the two new reader members, and config validation for Slice 3.
public class CartSupportTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_cart_tables_constraints_and_indexes_exist_and_the_scope_check_refuses_an_unknown_scope()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        List<string> checks = await f.Env.Db.Database.SqlQueryRaw<string>("SELECT conname AS \"Value\" FROM pg_constraint WHERE contype = 'c'").ToListAsync(ct);
        Assert.Contains("ck_cart_items_quantity", checks);
        Assert.Contains("ck_idempotency_keys_scope", checks);
        List<string> indexes = await f.Env.Db.Database.SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync(ct);
        Assert.Contains(indexes, d => d.Contains("UNIQUE INDEX ux_carts_user_id", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("UNIQUE INDEX ux_cart_items_cart_variant", StringComparison.Ordinal) && d.Contains("(cart_id, product_variant_id)", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("pk_idempotency_keys", StringComparison.Ordinal) && d.Contains("(scope, key)", StringComparison.Ordinal));

        PostgresException bad = await Assert.ThrowsAsync<PostgresException>(() => f.ExecAsync(
            "INSERT INTO idempotency_keys (scope, key, request_hash, created_at, expires_at) VALUES ('bogus', 'k', 'h', now(), now())"));
        Assert.Equal("ck_idempotency_keys_scope", bad.ConstraintName);
        PostgresException zero = await Assert.ThrowsAsync<PostgresException>(() => f.ExecAsync(
            "INSERT INTO carts (id, user_id, updated_at) VALUES (@c, @u, now()); " +
            "INSERT INTO cart_items (id, cart_id, product_variant_id, quantity, display_unit_price, currency, added_at, updated_at) VALUES (@i, @c, @v, 0, 1, 'USD', now(), now())",
            ("c", Guid.NewGuid()), ("u", Guid.NewGuid()), ("i", Guid.NewGuid()), ("v", Guid.NewGuid())));
        Assert.Equal("ck_cart_items_quantity", zero.ConstraintName);
    }

    [Fact]
    public async Task Variant_snapshots_carry_current_price_the_active_flag_and_the_lowest_sort_order_image_and_omit_unknown_ids()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres, new Dictionary<string, string> { ["Catalog:Images:BaseUrl"] = "https://img.example.test" });
        Guid withImages = await f.VariantAsync("SN-1");
        Guid noImages = await f.VariantAsync("SN-2");
        Guid inactive = await f.VariantAsync("SN-3");
        await f.DeactivateVariantAsync(inactive);
        await f.SetPriceAsync(withImages, 12.5m);
        Guid product = Guid.Parse((await f.Env.Db.Database.SqlQueryRaw<string>(
            "SELECT product_id::text AS \"Value\" FROM product_variants WHERE id = {0}", withImages).ToListAsync(TestContext.Current.CancellationToken))[0]);
        await f.ExecAsync(
            "INSERT INTO product_images (id, product_id, storage_key, alt_text, sort_order) VALUES (@a, @p, 'second.jpg', 'Second', 2), (@b, @p, 'first.jpg', 'First', 1)",
            ("a", Guid.NewGuid()), ("b", Guid.NewGuid()), ("p", product));
        using IServiceScope scope = f.Env.Factory.Services.CreateScope();
        IProductReader reader = scope.ServiceProvider.GetRequiredService<IProductReader>();

        IReadOnlyList<VariantSnapshot> found = await reader.GetVariantSnapshotsAsync([withImages, noImages, inactive, Guid.NewGuid()], TestContext.Current.CancellationToken);

        Assert.Equal(3, found.Count);
        VariantSnapshot a = found.Single(s => s.VariantId == withImages);
        Assert.Equal((12.5m, "USD", true, "p-sn-1"), (a.Price, a.Currency, a.IsActive, a.ProductSlug));
        Assert.Equal(new CartImage("https://img.example.test/first.jpg", "First"), a.Image);
        Assert.Null(found.Single(s => s.VariantId == noImages).Image);
        Assert.False(found.Single(s => s.VariantId == inactive).IsActive);
        Assert.Empty(await reader.GetVariantSnapshotsAsync([], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Availability_is_one_entry_per_id_with_no_row_as_zero_and_Out_and_reserved_stock_taken_off()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid stocked = await f.VariantAsync("AV-1", 9);
        Guid reserved = await f.VariantAsync("AV-2", 0);
        Guid none = await f.VariantAsync("AV-3", 0);
        await InventoryEnv.SetStockAsync(f.Env, reserved, 3, 3);
        using IServiceScope scope = f.Env.Factory.Services.CreateScope();
        IInventoryReader reader = scope.ServiceProvider.GetRequiredService<IInventoryReader>();

        IReadOnlyDictionary<Guid, StockAvailability> result = await reader.GetAvailabilityAsync([stocked, reserved, none, none], TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.Equal(new StockAvailability(9, StockStatus.InStock), result[stocked]);
        Assert.Equal(new StockAvailability(0, StockStatus.Out), result[reserved]);
        Assert.Equal(new StockAvailability(0, StockStatus.Out), result[none]);
    }

    [Theory]
    [InlineData("Cart:MaxQuantityPerLine", "0")]
    [InlineData("Cart:MaxQuantityPerLine", "101")]
    [InlineData("Store:Currency", "usd")]
    [InlineData("Store:Currency", "US")]
    public void An_invalid_cart_or_store_setting_stops_the_host_from_starting(string key, string value)
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable, settings: new Dictionary<string, string> { [key] = value });

        Exception? ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        string text = string.Join(" | ", Enumerable.Range(0, 1).SelectMany(_ => Chain(ex)));
        Assert.Contains(key, text);
    }

    private static IEnumerable<string> Chain(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            yield return ex.Message;
        }
    }
}
