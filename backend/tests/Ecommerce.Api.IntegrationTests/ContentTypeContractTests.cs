using System.Net;
using System.Net.Http.Headers;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Ecommerce.Identity.Domain;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests;

// CT1 .. CT4: a body route answers a missing or wrong Content-Type like MVC did (415, or 400 with no body at all), after authorization.
public class ContentTypeContractTests(PostgresFixture postgres)
{
    private const string UnsupportedMediaType = "https://tools.ietf.org/html/rfc9110#section-15.5.16";
    private const string ProblemJson = "application/problem+json";
    private const string SnapshotSql =
        "SELECT (SELECT count(*) FROM users) * 1000000 + (SELECT count(*) FROM categories) * 10000 + (SELECT count(*) FROM products) * 100 " +
        "+ (SELECT coalesce(sum(quantity_on_hand), 0) FROM inventory_stock)";

    public static TheoryData<string, string, string> AuthRoutes => new()
    {
        { "POST", "/api/v1/auth/register", """{"email":"new@example.test","displayName":"New","password":"correct horse battery"}""" },
        { "POST", "/api/v1/auth/login", """{"email":"ana@example.test","password":"correct horse battery"}""" },
        { "POST", "/api/v1/auth/password-reset", """{"email":"ana@example.test"}""" },
        { "POST", "/api/v1/auth/password-reset/confirm", """{"token":"x","newPassword":"correct horse battery"}""" },
    };

    public static TheoryData<string, string, string> AdminRoutes => new()
    {
        { "POST", "/api/v1/admin/categories", """{"slug":"c","name":"C"}""" },
        { "PUT", "/api/v1/admin/categories/c", """{"slug":"c","name":"C"}""" },
        { "POST", "/api/v1/admin/products", """{"name":"P"}""" },
        { "PUT", "/api/v1/admin/products/0195f2f0-0000-7000-8000-000000000001", """{"name":"P"}""" },
        { "POST", "/api/v1/admin/inventory/adjust", """{"variantId":"0195f2f0-0000-7000-8000-000000000001","delta":1,"expectedOnHand":5,"reason":"x"}""" },
    };

    private static async Task<IdentityTestEnv.Api> SendAsync(IdentityTestEnv env, string method, string url, string? json, string? contentType, string? bearer = null)
    {
        using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), url);
        if (json is not null)
        {
            request.Content = new StringContent(json);
            request.Content.Headers.ContentType = contentType is null ? null : MediaTypeHeaderValue.Parse(contentType);
        }
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        HttpResponseMessage response = await env.Client.SendAsync(request, TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new IdentityTestEnv.Api(response, System.Text.Json.JsonDocument.Parse(text.Length == 0 ? "{}" : text), text);
    }

    private static void AssertUnsupported(IdentityTestEnv.Api r)
    {
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, r.Status);
        Assert.Equal(ProblemJson, r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(UnsupportedMediaType, r.Str("type"));
    }

    // 415 with no Content-Type, 415 with text/plain, 400 with no body at all.
    private static async Task AssertThreeCasesAsync(IdentityTestEnv env, string method, string url, string json, string? bearer)
    {
        AssertUnsupported(await SendAsync(env, method, url, json, null, bearer));
        AssertUnsupported(await SendAsync(env, method, url, json, "text/plain", bearer));
        IdentityTestEnv.Api noBody = await SendAsync(env, method, url, null, null, bearer);
        Assert.Equal(HttpStatusCode.BadRequest, noBody.Status);
        Assert.Equal(ProblemJson, noBody.Response.Content.Headers.ContentType?.MediaType);
    }

    private static Task<long> SnapshotAsync(IdentityTestEnv env) => InventoryEnv.CountAsync(env, SnapshotSql);

    [Theory]
    [MemberData(nameof(AuthRoutes))]
    public async Task CT1_auth_body_routes_answer_415_or_400_and_do_nothing(string method, string url, string json)
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres, countHashing: true);
        await env.AddUserAsync("ana@example.test");
        long before = await SnapshotAsync(env);
        int verifies = env.Counter!.Verifies;

        await AssertThreeCasesAsync(env, method, url, json, null);

        Assert.Equal(before, await SnapshotAsync(env));
        Assert.Equal(verifies, env.Counter.Verifies);
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task CT2_admin_body_routes_answer_415_or_400_to_an_admin_and_401_to_anonymous(string method, string url, string json)
    {
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        await env.AddUserAsync("admin@example.test", UserRole.Admin);
        string token = await env.AccessTokenAsync("admin@example.test");
        Guid variant = await InventoryEnv.AddVariantAsync(env, "CT2");
        await InventoryEnv.SetStockAsync(env, variant, 5);
        long before = await SnapshotAsync(env);

        await AssertThreeCasesAsync(env, method, url, json, token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(env, method, url, json, null)).Status);

        Assert.Equal(before, await SnapshotAsync(env));
    }

    [Fact]
    public async Task CT3_logout_with_a_json_body_and_no_content_type_is_415()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);

        AssertUnsupported(await SendAsync(env, "POST", "/api/v1/auth/logout", """{"refreshToken":"x"}""", null));
    }

    [Fact]
    public void CT4_every_json_body_endpoint_also_accepts_any_content_type()
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);

        List<RouteEndpoint> withBody = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetOrderedMetadata<IAcceptsMetadata>().Any(a => a.ContentTypes.Contains("application/json")))
            .ToList();

        Assert.True(withBody.Count >= 11, $"expected at least 11 body endpoints, found {withBody.Count}");
        string[] missing = withBody
            .Where(e => !e.Metadata.GetOrderedMetadata<IAcceptsMetadata>()[^1].ContentTypes.Contains("*/*"))
            .Select(e => e.RoutePattern.RawText!)
            .ToArray();
        Assert.Empty(missing);
    }
}
