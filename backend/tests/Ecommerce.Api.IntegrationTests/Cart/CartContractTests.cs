using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Cart;

// CC1 .. CC7: the HTTP shape of the cart endpoints (409 bodies, 404, validation 400s, unreadable bodies, money and shape).
public class CartContractTests(PostgresFixture postgres)
{
    private const string TypeBadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string TypeNotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string TypeConflict = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    private static void AssertProblem(IdentityTestEnv.Api r, HttpStatusCode status, string type)
    {
        Assert.Equal(status, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(type, r.Str("type"));
        Assert.False(string.IsNullOrEmpty(r.Str("traceId")));
        Assert.Equal("id-test-1", r.Str("correlationId"));
    }

    private static void AssertValidation(IdentityTestEnv.Api r, string expectedErrors)
    {
        AssertProblem(r, HttpStatusCode.BadRequest, TypeBadRequest);
        Assert.Equal(ValidationTitle, r.Str("title"));
        Assert.Equal(expectedErrors, Errors(r));
    }

    // "key=message1,message2" per key, keys sorted, joined with " | ".
    private static string Errors(IdentityTestEnv.Api r) =>
        string.Join(" | ", r.Json.GetProperty("errors").EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => p.Name + "=" + string.Join(",", p.Value.EnumerateArray().Select(m => m.GetString()))));

    private static string[] Names(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();

    private static void AssertMoney(JsonElement money, string amount)
    {
        Assert.Equal(["amount", "currency"], Names(money));
        Assert.Equal(JsonValueKind.String, money.GetProperty("amount").ValueKind);
        Assert.Equal(amount, money.GetProperty("amount").GetString());
        Assert.Equal("USD", money.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task CC1_a_quantity_above_available_is_a_409_problem_with_available_and_no_errors_property()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("CC1", 4);
        await f.AddAsync(f.TokenA, v, 1);

        IdentityTestEnv.Api add = await f.AddAsync(f.TokenA, v, 4);
        IdentityTestEnv.Api set = await f.SetAsync(f.TokenA, v, 5);

        foreach (IdentityTestEnv.Api r in new[] { add, set })
        {
            AssertProblem(r, HttpStatusCode.Conflict, TypeConflict);
            Assert.Equal("Not enough stock", r.Str("title"));
            Assert.Equal("Only 4 available.", r.Str("detail"));
            Assert.Equal(4, r.Json.GetProperty("available").GetInt32());
            Assert.False(r.Json.TryGetProperty("errors", out JsonElement _));
        }
        Assert.Equal(1, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task CC2_another_users_line_is_a_404_on_put_a_no_op_on_delete_and_stays_untouched()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("CC2");
        await f.AddAsync(f.TokenA, v, 2);

        IdentityTestEnv.Api put = await f.SetAsync(f.TokenB, v, 1);
        IdentityTestEnv.Api delete = await f.DeleteAsync(f.TokenB, v);
        IdentityTestEnv.Api bCart = await f.GetAsync(f.TokenB);

        AssertProblem(put, HttpStatusCode.NotFound, TypeNotFound);
        Assert.Equal("Item not in cart", put.Str("title"));
        Assert.Equal(HttpStatusCode.NoContent, delete.Status);
        Assert.Empty(bCart.Json.GetProperty("items").EnumerateArray());
        Assert.Equal(2, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task CC3_the_request_validators_answer_400_with_the_field_keys_and_messages()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("CC3");
        await f.AddAsync(f.TokenA, v, 1);

        AssertValidation(await f.Env.PostAsync("/api/v1/cart/items", $$"""{"variantId":"{{EmptyGuid}}","quantity":0}""", f.TokenA),
            "quantity=Quantity must be 1 to 1000. | variantId=Choose an item.");
        AssertValidation(await f.Env.PostAsync("/api/v1/cart/items", "{}", f.TokenA),
            "quantity=Enter a quantity. | variantId='Variant Id' must not be empty.");
        AssertValidation(await f.Env.SendAsync(HttpMethod.Put, $"/api/v1/cart/items/{v}", "{}", f.TokenA),
            "quantity=Enter a quantity.");
        AssertValidation(await f.Env.SendAsync(HttpMethod.Put, $"/api/v1/cart/items/{v}", """{"quantity":1001}""", f.TokenA),
            "quantity=Quantity cannot be negative.");

        Assert.Equal(1, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task CC4_the_service_rejections_are_400_keyed_by_the_field()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("CC4", 50);

        AssertValidation(await f.AddAsync(f.TokenA, Guid.NewGuid(), 1), "variantId=This item is not available.");
        Assert.Empty(await f.QuantitiesAsync(f.TokenA));

        await f.AddAsync(f.TokenA, v, 8);
        AssertValidation(await f.AddAsync(f.TokenA, v, 3), "quantity=A line can hold 1 to 10 of an item.");
        Assert.Equal(8, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task CC5_a_merge_checks_the_key_first_then_the_body_and_a_reused_key_is_a_409_problem()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("CC5");

        AssertValidation(await f.MergeRawAsync(f.TokenA, null, """{"lines":[]}"""),
            "Idempotency-Key=Send an Idempotency-Key header of 1 to 100 characters.");
        AssertValidation(await f.MergeRawAsync(f.TokenA, CartEnv.Key, """{"lines":[]}"""),
            "lines=Send 1 to 100 lines.");

        Assert.Equal(HttpStatusCode.OK, (await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 1))).Status);
        IdentityTestEnv.Api reused = await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 2));

        AssertProblem(reused, HttpStatusCode.Conflict, TypeConflict);
        Assert.Equal("Conflict", reused.Str("title"));
        Assert.Equal("This Idempotency-Key was already used for a different request.", reused.Str("detail"));
        Assert.False(reused.Json.TryGetProperty("errors", out JsonElement _));
        Assert.Equal(1, (await f.QuantitiesAsync(f.TokenA))[v]);
    }

    [Fact]
    public async Task CC6_an_unreadable_body_is_a_400_problem_and_changes_nothing()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("CC6");
        await f.AddAsync(f.TokenA, v, 1);

        foreach (string body in new[] { $$"""{"variantId":"{{v}}","quantity":""", "null", $$"""{"variantId":"{{v}}","quantity":"abc"}""" })
        {
            AssertProblem(await f.Env.PostAsync("/api/v1/cart/items", body, f.TokenA), HttpStatusCode.BadRequest, TypeBadRequest);
        }
        foreach (string body in new[] { """{"quantity":""", "null", """{"quantity":"abc"}""" })
        {
            AssertProblem(await f.Env.SendAsync(HttpMethod.Put, $"/api/v1/cart/items/{v}", body, f.TokenA), HttpStatusCode.BadRequest, TypeBadRequest);
        }
        foreach (string body in new[] { """{"lines":""", "null", """{"lines":"abc"}""" })
        {
            AssertProblem(await f.MergeRawAsync(f.TokenA, CartEnv.Key, body), HttpStatusCode.BadRequest, TypeBadRequest);
        }

        Assert.Equal(1, (await f.QuantitiesAsync(f.TokenA))[v]);
        Assert.Equal(HttpStatusCode.OK, (await f.MergeAsync(f.TokenA, CartEnv.Key, (v, 1))).Status);
    }

    [Fact]
    public async Task CC7_money_is_a_string_the_shapes_are_exact_and_no_cookie_is_set()
    {
        await using CartEnv f = await CartEnv.StartAsync(postgres);
        Guid v = await f.VariantAsync("CC7");

        IdentityTestEnv.Api empty = await f.GetAsync(f.TokenA);
        Assert.Equal("""{"items":[],"subtotal":{"amount":"0.00","currency":"USD"}}""", empty.Text);

        IdentityTestEnv.Api add = await f.AddAsync(f.TokenA, v, 2);
        JsonElement line = CartEnv.Line(add, v);
        Assert.Equal(
            ["image", "issues", "lineTotal", "productName", "productSlug", "quantity", "stockStatus", "unitPrice", "variantId", "variantName"],
            Names(line));
        Assert.Equal(JsonValueKind.Null, line.GetProperty("image").ValueKind);
        AssertMoney(line.GetProperty("unitPrice"), "10.00");
        AssertMoney(line.GetProperty("lineTotal"), "20.00");
        AssertMoney(add.Json.GetProperty("subtotal"), "20.00");

        IdentityTestEnv.Api merge = await f.MergeAsync(f.TokenA, CartEnv.Key, (Guid.NewGuid(), 1));
        Assert.Equal(HttpStatusCode.OK, merge.Status);
        Assert.Equal(["cart", "results"], Names(merge.Json));
        JsonElement result = merge.Json.GetProperty("results").EnumerateArray().Single();
        Assert.Equal(["outcome", "quantity", "reason", "variantId"], Names(result));
        Assert.Equal("Removed", result.GetProperty("outcome").GetString());
        Assert.Equal("Unavailable", result.GetProperty("reason").GetString());

        foreach (IdentityTestEnv.Api r in new[] { empty, add, merge })
        {
            Assert.False(r.Response.Headers.Contains("Set-Cookie"));
        }
    }
}
