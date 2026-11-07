using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Cart;

// Test 21: server-owned cart writes, bounds, re-pricing and flags, and per-user isolation.
public class CartApiTests(PostgresFixture postgres)
{
    private static string Money(JsonElement m) => m.GetProperty("amount").GetString()! + " " + m.GetProperty("currency").GetString();

    [Fact]
    public async Task T21_add_sums_into_the_line_set_replaces_it_and_every_reply_is_the_whole_cart()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-1", 30);

        IdentityTestEnv.Api first = await f.AddAsync(f.TokenA, v, 2);
        IdentityTestEnv.Api second = await f.AddAsync(f.TokenA, v, 3);
        IdentityTestEnv.Api set = await f.SetAsync(f.TokenA, v, 4);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(5, CartEnv.Line(second, v).GetProperty("quantity").GetInt32());
        JsonElement line = CartEnv.Line(set, v);
        Assert.Equal(4, line.GetProperty("quantity").GetInt32());
        Assert.Equal("10.00 USD", Money(line.GetProperty("unitPrice")));
        Assert.Equal("40.00 USD", Money(line.GetProperty("lineTotal")));
        Assert.Equal("40.00 USD", Money(set.Json.GetProperty("subtotal")));
        Assert.Equal("p-c-1", line.GetProperty("productSlug").GetString());
        Assert.Equal("InStock", line.GetProperty("stockStatus").GetString());
        Assert.Empty(CartEnv.Issues(line));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task T21_an_add_whose_resulting_quantity_is_below_1_or_above_the_cap_is_400_and_changes_nothing(int quantity)
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-2", 50);
        await f.AddAsync(f.TokenA, v, 2);

        Assert.Equal(HttpStatusCode.BadRequest, (await f.AddAsync(f.TokenA, v, quantity)).Status);

        Assert.Equal(2, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task T21_an_add_that_pushes_the_line_over_the_cap_is_400_and_a_set_over_the_cap_or_below_zero_is_400()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-3", 50);
        await f.AddAsync(f.TokenA, v, 8);

        Assert.Equal(HttpStatusCode.BadRequest, (await f.AddAsync(f.TokenA, v, 3)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.SetAsync(f.TokenA, v, 11)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.SetAsync(f.TokenA, v, -1)).Status);

        Assert.Equal(8, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task T21_a_body_carrying_a_price_is_400_and_changes_nothing()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-4");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.Env.PostAsync("/api/v1/cart/items", new { variantId = v, quantity = 1, unitPrice = "0.01" }, f.TokenA)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Env.PostAsync("/api/v1/cart/items", new { variantId = v, quantity = 1, price = "0.01" }, f.TokenA)).Status);

        Assert.Empty(await f.QuantitiesAsync(f.TokenA));
    }

    [Fact]
    public async Task T21_a_quantity_above_available_is_409_with_available_and_changes_nothing()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-5", 4);
        await f.AddAsync(f.TokenA, v, 1);

        IdentityTestEnv.Api add = await f.AddAsync(f.TokenA, v, 4);
        IdentityTestEnv.Api set = await f.SetAsync(f.TokenA, v, 5);

        Assert.Equal(HttpStatusCode.Conflict, add.Status);
        Assert.Equal(4, add.Json.GetProperty("available").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, set.Status);
        Assert.Equal(4, set.Json.GetProperty("available").GetInt32());
        Assert.Equal(1, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task T21_an_inactive_product_inactive_variant_unknown_variant_or_other_currency_is_400()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid product = await f.VariantAsync("C-6");
        Guid variant = await f.VariantAsync("C-7");
        Guid euro = await f.VariantAsync("C-8");
        await f.DeactivateProductAsync(product);
        await f.DeactivateVariantAsync(variant);
        await f.SetPriceAsync(euro, 5m, "EUR");

        foreach (Guid id in new[] { product, variant, euro, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await f.AddAsync(f.TokenA, id, 1)).Status);
        }
        Assert.Empty(await f.QuantitiesAsync(f.TokenA));
    }

    [Fact]
    public async Task T21_put_of_an_absent_line_is_404_and_never_creates_it()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-9");

        Assert.Equal(HttpStatusCode.NotFound, (await f.SetAsync(f.TokenA, v, 2)).Status);

        Assert.Empty(await f.QuantitiesAsync(f.TokenA));
    }

    [Fact]
    public async Task T21_put_zero_and_delete_remove_the_line_and_delete_of_an_absent_line_is_204()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid a = await f.VariantAsync("C-10");
        Guid b = await f.VariantAsync("C-11");
        await f.AddAsync(f.TokenA, a, 2);
        await f.AddAsync(f.TokenA, b, 2);

        IdentityTestEnv.Api zero = await f.SetAsync(f.TokenA, a, 0);
        Assert.Equal(HttpStatusCode.OK, zero.Status);
        Assert.Equal([b], (await f.QuantitiesAsync(f.TokenA)).Keys.ToArray());

        Assert.Equal(HttpStatusCode.NoContent, (await f.DeleteAsync(f.TokenA, b)).Status);
        Assert.Empty(await f.QuantitiesAsync(f.TokenA));
        Assert.Equal(HttpStatusCode.NoContent, (await f.DeleteAsync(f.TokenA, b)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await f.DeleteAsync(f.TokenA, Guid.NewGuid())).Status);
    }

    [Fact]
    public async Task T21_a_price_change_shows_the_current_price_with_PriceChanged_and_the_next_write_refreshes_the_display_price()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-12");
        await f.AddAsync(f.TokenA, v, 2);
        await f.SetPriceAsync(v, 12.50m);

        IdentityTestEnv.Api read = await f.GetAsync(f.TokenA);
        JsonElement line = CartEnv.Line(read, v);
        Assert.Equal("12.50 USD", Money(line.GetProperty("unitPrice")));
        Assert.Equal("25.00 USD", Money(line.GetProperty("lineTotal")));
        Assert.Equal(["PriceChanged"], CartEnv.Issues(line));
        Assert.Equal("25.00 USD", Money(read.Json.GetProperty("subtotal")));

        IdentityTestEnv.Api written = await f.SetAsync(f.TokenA, v, 2);
        Assert.Equal("12.50 USD", Money(CartEnv.Line(written, v).GetProperty("unitPrice")));
        Assert.Empty(CartEnv.Issues(CartEnv.Line(await f.GetAsync(f.TokenA), v)));
    }

    [Fact]
    public async Task T21_lowered_stock_flags_ExceedsStock_and_an_unpublished_product_flags_Unavailable_and_leaves_the_subtotal()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid low = await f.VariantAsync("C-13", 10);
        Guid gone = await f.VariantAsync("C-14", 10);
        await f.AddAsync(f.TokenA, low, 5);
        await f.AddAsync(f.TokenA, gone, 3);
        await f.ExecAsync("UPDATE inventory_stock SET quantity_on_hand = 2 WHERE product_variant_id = @id", ("id", low));
        await f.DeactivateProductAsync(gone);

        IdentityTestEnv.Api read = await f.GetAsync(f.TokenA);

        Assert.Equal(["ExceedsStock"], CartEnv.Issues(CartEnv.Line(read, low)));
        Assert.Equal(["Unavailable"], CartEnv.Issues(CartEnv.Line(read, gone)));
        Assert.Equal("50.00 USD", Money(read.Json.GetProperty("subtotal")));
    }

    [Fact]
    public async Task T21_a_read_of_a_user_with_no_cart_is_an_empty_cart_and_writes_no_row()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);

        IdentityTestEnv.Api read = await f.GetAsync(f.TokenB);

        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Empty(read.Json.GetProperty("items").EnumerateArray());
        Assert.Equal("0.00 USD", Money(read.Json.GetProperty("subtotal")));
        Assert.Equal(0, await Inventory.InventoryEnv.CountAsync(f.Env, "SELECT count(*) FROM carts"));
    }

    [Fact]
    public async Task T21_all_five_endpoints_are_401_for_anonymous_callers()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-15");

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.GetAsync(null)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.AddAsync(null, v, 1)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.SetAsync(null, v, 1)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.DeleteAsync(null, v)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.MergeAsync(null, CartEnv.Key, (v, 1))).Status);
    }

    [Fact]
    public async Task T21_one_users_writes_never_appear_in_another_users_cart()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-16");
        await f.AddAsync(f.TokenA, v, 3);
        await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 1));

        Assert.Empty(await f.QuantitiesAsync(f.TokenB));
        await f.DeleteAsync(f.TokenB, v);
        Assert.Equal(4, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task Concurrent_adds_of_the_same_variant_to_an_existing_cart_end_at_the_sum_not_the_last_write()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("C-17", 50);
        // The cart row exists first, so the race is on the lines and not on creating the cart.
        await f.AddAsync(f.TokenA, await f.VariantAsync("C-18"), 1);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<IdentityTestEnv.Api>[] calls = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return await f.AddAsync(f.TokenA, v, 2);
        })).ToArray();
        gate.SetResult();

        IdentityTestEnv.Api[] replies = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Equal(8, (await f.QuantitiesAsync(f.TokenA))[v]);
    }
}
