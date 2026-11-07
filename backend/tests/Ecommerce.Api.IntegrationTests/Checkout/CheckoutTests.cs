using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;

namespace Ecommerce.Api.IntegrationTests.Checkout;

/// <summary>Slice 4 checkout: tests 1, 2, 5, 6, 23 and the endpoint contract (arch 8.2, 8.3, 9, 11.2).</summary>
public class CheckoutTests(PostgresFixture postgres)
{
    private static string Key() => "key-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task T1_ten_users_race_for_the_last_unit_through_POST_checkout_one_wins_nine_get_OutOfStock()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("RACE-1", stock: 1);
        List<string> tokens = [];
        for (int i = 0; i < 10; i++)
        {
            string email = $"racer{i}@example.test";
            await f.Env.AddUserAsync(email);
            string token = await f.Env.AccessTokenAsync(email);
            Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(token, variant, 1)).Status);
            tokens.Add(token);
        }

        IdentityTestEnv.Api[] results = await Task.WhenAll(tokens.Select(t => f.CheckoutAsync(t, Key())));

        Assert.Single(results, r => r.Status == HttpStatusCode.OK);
        List<IdentityTestEnv.Api> losers = results.Where(r => r.Status != HttpStatusCode.OK).ToList();
        Assert.Equal(9, losers.Count);
        foreach (IdentityTestEnv.Api loser in losers)
        {
            Assert.Equal(HttpStatusCode.Conflict, loser.Status);
            Assert.Equal("checkout-lines-rejected", loser.Str("type"));
            JsonElement line = Assert.Single(loser.Json.GetProperty("lines").EnumerateArray().ToList());
            Assert.Equal(variant, line.GetProperty("variantId").GetGuid());
            Assert.Equal("OutOfStock", line.GetProperty("reason").GetString());
            Assert.Equal(0, line.GetProperty("available").GetInt32());
        }
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM inventory_reservations WHERE status = 'Active'"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal((1, 1), await InventoryEnv.ReadAsync(f.Env, variant));
    }

    [Fact]
    public async Task T2_the_same_key_twice_in_a_row_makes_one_order_one_payment_one_intent_with_identical_bodies()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("IDEM-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 2)).Status);
        string key = Key();

        IdentityTestEnv.Api first = await f.CheckoutAsync(f.TokenA, key);
        IdentityTestEnv.Api second = await f.CheckoutAsync(f.TokenA, key);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(first.Text, second.Text);
        Assert.False(string.IsNullOrEmpty(first.Str("clientSecret")));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments"));
        Assert.Single(f.Gateway.Intents);
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM inventory_reservations"));
    }

    [Fact]
    public async Task T2_the_same_key_sent_concurrently_makes_one_order_one_payment_one_intent_with_identical_bodies()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("IDEM-2", stock: 2); // exactly the quantity: a duplicate that re-reserved would find nothing left
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 2)).Status);
        string key = Key();

        IdentityTestEnv.Api[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.CheckoutAsync(f.TokenA, key)));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Single(results.Select(r => r.Text).Distinct());
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments"));
        Assert.Single(f.Gateway.Intents);
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM inventory_reservations"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM idempotency_keys WHERE scope = 'checkout'"));
    }

    [Fact]
    public async Task T2_the_same_key_with_another_body_or_another_user_is_a_409_and_creates_nothing()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres,
            new Dictionary<string, string> { ["Shipping:Methods:1:Code"] = "express", ["Shipping:Methods:1:Name"] = "Express", ["Shipping:Methods:1:Amount"] = "12.00", ["Shipping:Methods:1:EstimatedDays"] = "2" });
        Guid variant = await f.Cart.VariantAsync("IDEM-3");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenB, variant, 1)).Status);
        string key = Key();
        Assert.Equal(HttpStatusCode.OK, (await f.CheckoutAsync(f.TokenA, key)).Status);

        IdentityTestEnv.Api otherBody = await f.CheckoutAsync(f.TokenA, key, CheckoutEnv.Body("express"));
        IdentityTestEnv.Api otherUser = await f.CheckoutAsync(f.TokenB, key);

        Assert.Equal(HttpStatusCode.Conflict, otherBody.Status);
        Assert.Equal(HttpStatusCode.Conflict, otherUser.Status);
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM orders"));
    }

    [Fact]
    public async Task T5_a_body_carrying_a_price_total_tax_or_status_is_a_400_and_creates_nothing()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("BODY-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);
        object address = new { fullName = "Test Buyer", line1 = "1 Example Street", city = "Exampleville", postalCode = "12345", country = "US" };

        foreach (object body in new object[]
        {
            new { shippingAddress = address, shippingMethodCode = "standard", grandTotal = 0.01 },
            new { shippingAddress = address, shippingMethodCode = "standard", price = 0.01 },
            new { shippingAddress = address, shippingMethodCode = "standard", taxTotal = 0 },
            new { shippingAddress = address, shippingMethodCode = "standard", shippingTotal = 0 },
            new { shippingAddress = address, shippingMethodCode = "standard", status = "Paid" },
        })
        {
            IdentityTestEnv.Api r = await f.CheckoutAsync(f.TokenA, Key(), body);
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        }
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM inventory_reservations"));
    }

    [Fact]
    public async Task T5_the_gateway_amount_is_the_server_total_in_minor_units_and_the_response_matches()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("AMT-1"); // 10.00 each
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 2)).Status);

        IdentityTestEnv.Api r = await f.CheckoutAsync(f.TokenA, Key());

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement order = r.Json.GetProperty("order");
        Assert.Equal("20.00", order.GetProperty("subtotal").GetProperty("amount").GetString());
        Assert.Equal("5.00", order.GetProperty("shippingTotal").GetProperty("amount").GetString());
        Assert.Equal("0.00", order.GetProperty("taxTotal").GetProperty("amount").GetString());
        Assert.Equal("25.00", order.GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.Equal("PendingPayment", order.GetProperty("status").GetString());
        FakeIntent intent = Assert.Single(f.Gateway.Intents);
        Assert.Equal(2500, intent.Request.AmountMinor);
        Assert.Equal("USD", intent.Request.Currency);
        Assert.Equal(intent.Secret, r.Str("clientSecret"));
    }

    [Fact]
    public async Task T6_a_later_admin_price_change_leaves_the_item_snapshot_and_totals_unchanged()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("SNAP-1");
        Guid orderId = await f.PlaceOrderAsync(variant, 1, Key());

        await f.Cart.SetPriceAsync(variant, 99.00m);
        IdentityTestEnv.Api detail = await f.OrderAsync(f.TokenA, orderId);

        Assert.Equal(HttpStatusCode.OK, detail.Status);
        JsonElement item = Assert.Single(detail.Json.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("10.00", item.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal("15.00", detail.Json.GetProperty("grandTotal").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task T23_with_a_tax_rate_the_totals_follow_the_rounding_rule_and_the_intent_is_the_grand_total()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres, new Dictionary<string, string> { ["Tax:RatePercent"] = "10" });
        Guid variant = await f.Cart.VariantAsync("TAX-1");
        await f.Cart.SetPriceAsync(variant, 10.05m); // 10.05 x 10 % = 1.005: half away from zero is 1.01, banker rounding 1.00
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);

        IdentityTestEnv.Api r = await f.CheckoutAsync(f.TokenA, Key());

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement order = r.Json.GetProperty("order");
        Assert.Equal("10.05", order.GetProperty("subtotal").GetProperty("amount").GetString());
        Assert.Equal("1.01", order.GetProperty("taxTotal").GetProperty("amount").GetString());
        Assert.Equal("5.00", order.GetProperty("shippingTotal").GetProperty("amount").GetString());
        Assert.Equal("16.06", order.GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.Equal(1606, Assert.Single(f.Gateway.Intents).Request.AmountMinor);
        Assert.Equal(16.06m, await f.ScalarAsync<decimal>("SELECT amount FROM payments"));
    }

    [Fact]
    public async Task T23_the_address_and_the_method_name_are_snapshotted_on_the_order()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("SNAP-2");
        Guid orderId = await f.PlaceOrderAsync(variant, 1, Key());

        IdentityTestEnv.Api detail = await f.OrderAsync(f.TokenA, orderId);

        Assert.Equal("standard", detail.Json.GetProperty("shippingMethod").GetProperty("code").GetString());
        Assert.Equal("Standard", detail.Json.GetProperty("shippingMethod").GetProperty("name").GetString());
        JsonElement address = detail.Json.GetProperty("shippingAddress");
        Assert.Equal("Test Buyer", address.GetProperty("fullName").GetString());
        Assert.Equal("1 Example Street", address.GetProperty("line1").GetString());
        Assert.Equal("US", address.GetProperty("country").GetString());
    }

    [Fact]
    public async Task T23_an_unknown_method_a_foreign_country_or_client_money_fields_are_400_and_create_nothing()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("BAD-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);

        IdentityTestEnv.Api unknownMethod = await f.CheckoutAsync(f.TokenA, Key(), CheckoutEnv.Body("teleport"));
        IdentityTestEnv.Api foreign = await f.CheckoutAsync(f.TokenA, Key(), CheckoutEnv.Body(country: "FR"));
        IdentityTestEnv.Api money = await f.CheckoutAsync(f.TokenA, Key(),
            new { shippingAddress = new { fullName = "A", line1 = "B", city = "C", postalCode = "1", country = "US" }, shippingMethodCode = "standard", shippingAmount = "0.00", total = "0.01" });

        Assert.Equal(HttpStatusCode.BadRequest, unknownMethod.Status);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.Status);
        Assert.Equal(HttpStatusCode.BadRequest, money.Status);
        Assert.True(unknownMethod.Json.GetProperty("errors").TryGetProperty("shippingMethodCode", out _));
        Assert.True(foreign.Json.GetProperty("errors").TryGetProperty("shippingAddress.country", out _));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM inventory_reservations"));
        Assert.Empty(f.Gateway.Creates);
    }

    // ---- endpoint contract ----

    [Fact]
    public async Task The_three_endpoints_answer_401_to_an_anonymous_caller()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CheckoutAsync(null, Key())).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.GetAsync("/api/v1/shipping-methods")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.RetryAsync(null, Guid.NewGuid(), Key())).Status);
    }

    [Fact]
    public async Task Shipping_methods_lists_the_configured_flat_rates()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);

        IdentityTestEnv.Api r = await f.Env.GetAsync("/api/v1/shipping-methods", f.TokenA);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        JsonElement item = Assert.Single(r.Json.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("standard", item.GetProperty("code").GetString());
        Assert.Equal("Standard", item.GetProperty("name").GetString());
        Assert.Equal("5.00", item.GetProperty("price").GetProperty("amount").GetString());
        Assert.Equal("USD", item.GetProperty("price").GetProperty("currency").GetString());
        Assert.Equal(5, item.GetProperty("estimatedDays").GetInt32());
    }

    [Fact]
    public async Task A_missing_or_oversized_idempotency_key_is_a_400()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("KEY-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);

        Assert.Equal(HttpStatusCode.BadRequest, (await f.CheckoutAsync(f.TokenA, null)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.CheckoutAsync(f.TokenA, new string('k', 101))).Status);
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM orders"));
    }

    [Fact]
    public async Task Address_rules_are_400_with_a_field_per_problem()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("ADDR-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);

        IdentityTestEnv.Api r = await f.CheckoutAsync(f.TokenA, Key(), new
        {
            shippingAddress = new { fullName = "  ", line1 = new string('x', 101), city = "", postalCode = new string('1', 21), country = "US" },
            shippingMethodCode = "standard",
        });
        IdentityTestEnv.Api none = await f.CheckoutAsync(f.TokenA, Key(), new { shippingMethodCode = "standard" });

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        JsonElement errors = r.Json.GetProperty("errors");
        foreach (string field in new[] { "shippingAddress.fullName", "shippingAddress.line1", "shippingAddress.city", "shippingAddress.postalCode" })
        {
            Assert.True(errors.TryGetProperty(field, out _), field);
        }
        Assert.Equal(HttpStatusCode.BadRequest, none.Status);
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM orders"));
    }

    [Fact]
    public async Task An_empty_cart_is_a_409_cart_empty()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);

        IdentityTestEnv.Api r = await f.CheckoutAsync(f.TokenA, Key());

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal("cart-empty", r.Str("type"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM idempotency_keys"));
    }

    [Fact]
    public async Task A_changed_price_and_an_unavailable_item_are_409_lines_and_nothing_is_reserved()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid moved = await f.Cart.VariantAsync("LINE-P");
        Guid gone = await f.Cart.VariantAsync("LINE-U");
        Guid fine = await f.Cart.VariantAsync("LINE-F");
        foreach (Guid v in new[] { moved, gone, fine })
        {
            Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, v, 1)).Status);
        }
        await f.Cart.SetPriceAsync(moved, 11.00m);
        await f.Cart.DeactivateVariantAsync(gone);

        IdentityTestEnv.Api r = await f.CheckoutAsync(f.TokenA, Key());

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal("checkout-lines-rejected", r.Str("type"));
        Dictionary<Guid, string> reasons = r.Json.GetProperty("lines").EnumerateArray()
            .ToDictionary(l => l.GetProperty("variantId").GetGuid(), l => l.GetProperty("reason").GetString()!);
        Assert.Equal(new Dictionary<Guid, string> { [moved] = "PriceChanged", [gone] = "Unavailable" }, reasons);
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM inventory_reservations"));
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM idempotency_keys"));
    }

    [Fact]
    public async Task A_cart_line_priced_in_another_currency_is_a_400()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("CUR-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);
        await f.Cart.SetPriceAsync(variant, 10.00m, "EUR");

        IdentityTestEnv.Api r = await f.CheckoutAsync(f.TokenA, Key());

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.Equal(0, await f.CountAsync("SELECT count(*) FROM orders"));
    }

    [Fact]
    public async Task A_provider_outage_leaves_a_PendingPayment_order_503_with_its_id_and_the_same_key_heals_it()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("OUT-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);
        string key = Key();
        f.Gateway.Unavailable = true;

        IdentityTestEnv.Api down = await f.CheckoutAsync(f.TokenA, key);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, down.Status);
        Assert.Equal("payment-provider-unavailable", down.Str("type"));
        Guid orderId = Guid.Parse(down.Str("orderId"));
        Assert.Equal("PendingPayment", await f.OrderStatusAsync(orderId));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments WHERE provider_payment_id IS NULL"));

        f.Gateway.Unavailable = false;
        IdentityTestEnv.Api healed = await f.CheckoutAsync(f.TokenA, key);

        Assert.Equal(HttpStatusCode.OK, healed.Status);
        Assert.Equal(orderId, healed.Json.GetProperty("order").GetProperty("id").GetGuid());
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM orders"));
        Assert.Equal(1, await f.CountAsync("SELECT count(*) FROM payments WHERE provider_payment_id IS NOT NULL"));
    }

    // ---- rule 6 ----

    [Fact]
    public async Task Rule6_the_gateway_never_sees_an_open_transaction_in_checkout_replay_or_retry()
    {
        await using CheckoutEnv f = await CheckoutEnv.StartAsync(postgres);
        Guid variant = await f.Cart.VariantAsync("TX-1");
        Assert.Equal(HttpStatusCode.OK, (await f.Cart.AddAsync(f.TokenA, variant, 1)).Status);
        string key = Key();

        IdentityTestEnv.Api first = await f.CheckoutAsync(f.TokenA, key);
        IdentityTestEnv.Api replay = await f.CheckoutAsync(f.TokenA, key);
        Guid orderId = first.Json.GetProperty("order").GetProperty("id").GetGuid();
        IdentityTestEnv.Api retry = await f.RetryAsync(f.TokenA, orderId, Key());

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(HttpStatusCode.OK, retry.Status);
        Assert.NotEmpty(f.Gateway.Creates);
        Assert.True(f.Gateway.Retrieves > 0);
        Assert.Equal(0, f.Gateway.TransactionViolations);
    }
}
