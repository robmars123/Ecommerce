using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Cart;

// Test 22: merge rules, results, and idempotency.
public class CartMergeTests(PostgresFixture postgres)
{
    private static (string Outcome, int Quantity, string? Reason) Result(IdentityTestEnv.Api merge, int index)
    {
        JsonElement r = merge.Json.GetProperty("results")[index];
        string? reason = r.GetProperty("reason").ValueKind == JsonValueKind.Null ? null : r.GetProperty("reason").GetString();
        return (r.GetProperty("outcome").GetString()!, r.GetProperty("quantity").GetInt32(), reason);
    }

    [Fact]
    public async Task T22_the_same_variant_sums_with_the_server_line_and_is_Kept()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("MG-1", 20);
        Assert.Equal(HttpStatusCode.OK, (await f.AddAsync(f.TokenA, v, 2)).Status);

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 3));

        Assert.Equal(HttpStatusCode.OK, merge.Status);
        Assert.Equal(("Kept", 5, null), Result(merge, 0));
        Assert.Equal(5, (await f.QuantitiesAsync(f.TokenA))[v]);
        Assert.Equal(5, merge.Json.GetProperty("cart").GetProperty("items")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task T22_a_sum_over_the_cap_is_Capped_at_the_cap_with_LimitReached()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("MG-2", 50);
        await f.AddAsync(f.TokenA, v, 8);

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 5));

        Assert.Equal(("Capped", 10, "LimitReached"), Result(merge, 0));
        Assert.Equal(10, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task T22_over_available_below_the_cap_is_Capped_with_OutOfStock()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("MG-3", 4);
        await f.AddAsync(f.TokenA, v, 1);

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 6));

        Assert.Equal(("Capped", 4, "OutOfStock"), Result(merge, 0));
        Assert.Equal(4, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task T22_when_available_is_not_above_the_server_quantity_the_guest_line_is_Removed_and_the_server_line_is_untouched()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("MG-4", 5);
        await f.AddAsync(f.TokenA, v, 5);
        await f.ExecAsync("UPDATE inventory_stock SET quantity_on_hand = 3 WHERE product_variant_id = @id", ("id", v));

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 2));

        Assert.Equal(("Removed", 5, "OutOfStock"), Result(merge, 0));
        Assert.Equal(5, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task T22_a_new_line_with_no_stock_is_Removed_OutOfStock_and_not_added()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("MG-5", 0);

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 2));

        Assert.Equal(("Removed", 0, "OutOfStock"), Result(merge, 0));
        Assert.Empty(await f.QuantitiesAsync(f.TokenA));
    }

    [Fact]
    public async Task T22_inactive_unknown_and_other_currency_lines_are_Removed_Unavailable()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid inactiveProduct = await f.VariantAsync("MG-6");
        Guid inactiveVariant = await f.VariantAsync("MG-7");
        Guid euro = await f.VariantAsync("MG-8");
        Guid good = await f.VariantAsync("MG-9");
        await f.DeactivateProductAsync(inactiveProduct);
        await f.DeactivateVariantAsync(inactiveVariant);
        await f.SetPriceAsync(euro, 10m, "EUR");
        Guid unknown = Guid.NewGuid();

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, (inactiveProduct, 1), (inactiveVariant, 1), (euro, 1), (unknown, 1), (good, 2));

        Assert.Equal(HttpStatusCode.OK, merge.Status);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(("Removed", 0, "Unavailable"), Result(merge, i));
        }
        Assert.Equal(("Kept", 2, null), Result(merge, 4));
        Assert.Equal([good], (await f.QuantitiesAsync(f.TokenA)).Keys.ToArray());
    }

    [Fact]
    public async Task T22_every_input_line_appears_once_in_input_order()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid[] v = [await f.VariantAsync("MO-1"), await f.VariantAsync("MO-2"), await f.VariantAsync("MO-3")];
        Guid[] order = [v[2], Guid.NewGuid(), v[0], v[1]];

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, order.Select(g => (g, 1)).ToArray());

        Assert.Equal(order, merge.Json.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("variantId").GetGuid()).ToArray());
    }

    [Fact]
    public async Task T22_the_same_key_twice_returns_the_same_body_and_does_not_double_quantities_even_after_an_edit()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("ID-1", 50);

        IdentityTestEnv.Api first = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 3));
        IdentityTestEnv.Api second = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 3));

        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(3, (await f.QuantitiesAsync(f.TokenA))[v]);

        Assert.Equal(HttpStatusCode.OK, (await f.SetAsync(f.TokenA, v, 7)).Status);
        IdentityTestEnv.Api third = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 3));
        Assert.Equal(first.Text, third.Text);
        Assert.Equal(7, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task T22_the_same_key_with_a_different_body_or_from_another_user_is_409_and_changes_nothing()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("ID-2", 50);
        await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 3));

        Assert.Equal(HttpStatusCode.Conflict, (await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 4))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await f.MergeAsync(f.TokenB, CartEnv.Key, (v, 3))).Status);

        Assert.Equal(3, (await f.QuantitiesAsync(f.TokenA))[v]);
        Assert.Empty(await f.QuantitiesAsync(f.TokenB));
    }

    [Fact]
    public async Task T22_a_missing_or_overlong_key_and_bad_bodies_are_400_and_write_nothing()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("BD-1");
        Guid w = await f.VariantAsync("BD-2");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeAsync(f.TokenA, null, (v, 1))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeAsync(f.TokenA, new string('k', 101), (v, 1))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeAsync(f.TokenA, CartEnv.Key)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 1), (v, 2))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 0))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 1001))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeAsync(f.TokenA, CartEnv.Key, Enumerable.Range(0, 101).Select(_ => (Guid.NewGuid(), 1)).ToArray())).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.MergeRawAsync(f.TokenA, CartEnv.Key, new { lines = new[] { new { variantId = w, quantity = 1, price = "1.00" } } })).Status);

        Assert.Empty(await f.QuantitiesAsync(f.TokenA));
        // None of those used the key: it is still free for a valid merge.
        Assert.Equal(HttpStatusCode.OK, (await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 1))).Status);
    }

    [Fact]
    public async Task T22_a_hundred_lines_are_accepted_and_the_anonymous_caller_is_401()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.OK, (await f.MergeAsync(f.TokenA, CartEnv.Key, Enumerable.Range(0, 100).Select(_ => (Guid.NewGuid(), 1)).ToArray())).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.MergeAsync(null, CartEnv.Key, (Guid.NewGuid(), 1))).Status);
    }

    [Fact]
    public async Task T22_the_same_key_sent_twice_in_parallel_applies_once_and_both_replies_are_identical()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("PR-1", 50);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<IdentityTestEnv.Api>[] calls = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 3));
        })).ToArray();
        gate.SetResult();

        IdentityTestEnv.Api[] replies = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Equal(replies[0].Text, replies[1].Text);
        Assert.Equal(3, (await f.QuantitiesAsync(f.TokenA))[v]);
    }
}
