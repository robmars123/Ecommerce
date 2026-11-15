using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Api.IntegrationTests.Inventory;

// IV1 .. IV7: the HTTP shape of the admin inventory endpoints (409 bodies, 404, validation 400s, query binding).
public class AdminInventoryContractTests(PostgresFixture postgres)
{
    private const string AdjustUrl = "/api/v1/admin/inventory/adjust";
    private const string ListUrl = "/api/v1/admin/inventory";
    private const string TypeBadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string TypeNotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string TypeConflict = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string ValidationTitle = "One or more validation errors occurred.";

    private async Task<(IdentityTestEnv Env, string Token)> StartAsync()
    {
        IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        return (env, await env.AccessTokenAsync("admin@example.test"));
    }

    private static void AssertProblem(IdentityTestEnv.Api r, HttpStatusCode status, string type)
    {
        Assert.Equal(status, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(type, r.Str("type"));
        Assert.False(string.IsNullOrEmpty(r.Str("traceId")));
        Assert.Equal("id-test-1", r.Str("correlationId"));
    }

    private static void AssertConflict(IdentityTestEnv.Api r, string detail, int onHand, int reserved, int available)
    {
        AssertProblem(r, HttpStatusCode.Conflict, TypeConflict);
        Assert.Equal("Conflict", r.Str("title"));
        Assert.Equal(detail, r.Str("detail"));
        Assert.Equal((onHand, reserved, available),
            (r.Json.GetProperty("onHand").GetInt32(), r.Json.GetProperty("reserved").GetInt32(), r.Json.GetProperty("available").GetInt32()));
        Assert.False(r.Json.TryGetProperty("errors", out JsonElement _));
    }

    private static string[] Keys(IdentityTestEnv.Api r) =>
        r.Json.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToArray();

    [Fact]
    public async Task IV1_a_stale_adjust_is_a_409_problem_with_the_counts_and_no_errors_property()
    {
        (IdentityTestEnv env, string token) = await StartAsync();
        await using IdentityTestEnv _ = env;
        Guid variant = await InventoryEnv.AddVariantAsync(env, "IV1");
        await InventoryEnv.SetStockAsync(env, variant, 5);

        IdentityTestEnv.Api r = await env.PostAsync(AdjustUrl, new { variantId = variant, delta = 1, expectedOnHand = 4, reason = "x" }, token);

        AssertConflict(r, "The on-hand count changed. Reload and try again.", 5, 0, 5);
        Assert.Equal((5, 0), await InventoryEnv.ReadAsync(env, variant));
    }

    [Fact]
    public async Task IV2_an_adjust_that_leaves_less_available_than_reserved_is_a_409_problem_with_the_counts()
    {
        (IdentityTestEnv env, string token) = await StartAsync();
        await using IdentityTestEnv _ = env;
        Guid variant = await InventoryEnv.AddVariantAsync(env, "IV2");
        await InventoryEnv.SetStockAsync(env, variant, 5);
        Assert.True((await InventoryEnv.ReserveAsync(env, Guid.NewGuid(), [new ReservationLine(variant, 3)])).Succeeded);

        IdentityTestEnv.Api r = await env.PostAsync(AdjustUrl, new { variantId = variant, delta = -3, expectedOnHand = 5, reason = "x" }, token);

        AssertConflict(r, "The change would leave less available than is reserved.", 5, 3, 2);
        Assert.Equal((5, 3), await InventoryEnv.ReadAsync(env, variant));
    }

    [Fact]
    public async Task IV3_an_unknown_variant_is_a_404_problem_and_writes_nothing()
    {
        (IdentityTestEnv env, string token) = await StartAsync();
        await using IdentityTestEnv _ = env;

        IdentityTestEnv.Api r = await env.PostAsync(AdjustUrl, new { variantId = Guid.NewGuid(), delta = 1, expectedOnHand = 0, reason = "x" }, token);

        AssertProblem(r, HttpStatusCode.NotFound, TypeNotFound);
        Assert.Equal("Variant not found", r.Str("title"));
        Assert.Equal(0, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock"));
    }

    [Fact]
    public async Task IV4_validation_400_has_the_title_and_the_four_error_keys_and_writes_nothing()
    {
        (IdentityTestEnv env, string token) = await StartAsync();
        await using IdentityTestEnv _ = env;

        IdentityTestEnv.Api bad = await env.SendAsync(HttpMethod.Post, AdjustUrl,
            "{\"variantId\":\"00000000-0000-0000-0000-000000000000\",\"delta\":0,\"expectedOnHand\":-1,\"reason\":\"  \"}", token);
        IdentityTestEnv.Api omitted = await env.SendAsync(HttpMethod.Post, AdjustUrl, "{}", token);

        foreach (IdentityTestEnv.Api r in new[] { bad, omitted })
        {
            AssertProblem(r, HttpStatusCode.BadRequest, TypeBadRequest);
            Assert.Equal(ValidationTitle, r.Str("title"));
            Assert.Equal(["delta", "expectedOnHand", "reason", "variantId"], Keys(r));
        }
        JsonElement errors = bad.Json.GetProperty("errors");
        Assert.Equal("Choose a variant.", errors.GetProperty("variantId")[0].GetString());
        Assert.Equal("The change must not be 0 and must be within 1,000,000 either way.", errors.GetProperty("delta")[0].GetString());
        Assert.Equal("The on-hand count cannot be negative.", errors.GetProperty("expectedOnHand")[0].GetString());
        Assert.Equal("Enter a reason of 1 to 500 characters.", errors.GetProperty("reason")[0].GetString());
        Assert.Equal(0, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock"));
    }

    [Fact]
    public async Task IV5_unreadable_bodies_are_400_problems_and_write_nothing()
    {
        (IdentityTestEnv env, string token) = await StartAsync();
        await using IdentityTestEnv _ = env;
        Guid variant = await InventoryEnv.AddVariantAsync(env, "IV5");
        string[] bodies =
        [
            "{\"delta\":",
            "",
            "null",
            $"{{\"variantId\":\"{variant}\",\"delta\":\"abc\",\"expectedOnHand\":1,\"reason\":\"x\"}}",
            $"{{\"variantId\":\"{variant}\",\"delta\":1,\"expectedOnHand\":1,\"reason\":\"x\",\"onHand\":99}}",
        ];

        foreach (string body in bodies)
        {
            AssertProblem(await env.SendAsync(HttpMethod.Post, AdjustUrl, body, token), HttpStatusCode.BadRequest, TypeBadRequest);
        }
        Assert.Equal(0, await InventoryEnv.CountAsync(env, "SELECT count(*) FROM inventory_stock"));
    }

    [Fact]
    public async Task IV6_a_bad_page_is_a_400_with_the_binding_message()
    {
        (IdentityTestEnv env, string token) = await StartAsync();
        await using IdentityTestEnv _ = env;

        IdentityTestEnv.Api parse = await env.GetAsync(ListUrl + "?page=abc", token);
        IdentityTestEnv.Api both = await env.GetAsync(ListUrl + "?page=abc&pageSize=0", token);
        IdentityTestEnv.Api range = await env.GetAsync(ListUrl + "?page=0&pageSize=0", token);

        foreach (IdentityTestEnv.Api r in new[] { parse, both, range })
        {
            AssertProblem(r, HttpStatusCode.BadRequest, TypeBadRequest);
            Assert.Equal(ValidationTitle, r.Str("title"));
        }
        Assert.Equal("The value 'abc' is not valid.", parse.Json.GetProperty("errors").GetProperty("page")[0].GetString());
        // Captured on MVC: a parse error stops the request before the range checks, so pageSize=0 is not reported.
        Assert.Equal(["page"], Keys(both));
        Assert.Equal("The value 'abc' is not valid.", both.Json.GetProperty("errors").GetProperty("page")[0].GetString());
        Assert.Equal(["page", "pageSize"], Keys(range));
    }

    [Fact]
    public async Task IV7_the_first_value_of_a_repeated_page_wins_and_blank_values_use_the_defaults()
    {
        (IdentityTestEnv env, string token) = await StartAsync();
        await using IdentityTestEnv _ = env;
        await InventoryEnv.AddProductAsync(env, "iv7", "Pager", ["P-1", "P-2", "P-3"]);

        IdentityTestEnv.Api repeated = await env.GetAsync(ListUrl + "?page=2&page=x&pageSize=2", token);
        IdentityTestEnv.Api blank = await env.GetAsync(ListUrl + "?page=&pageSize=", token);

        Assert.Equal(HttpStatusCode.OK, repeated.Status);
        Assert.Equal(2, repeated.Json.GetProperty("page").GetInt32());
        Assert.Single(repeated.Json.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, blank.Status);
        Assert.Equal((1, 20), (blank.Json.GetProperty("page").GetInt32(), blank.Json.GetProperty("pageSize").GetInt32()));
    }
}
