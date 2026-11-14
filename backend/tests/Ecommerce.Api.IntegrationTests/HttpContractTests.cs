using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ecommerce.Api.IntegrationTests.Catalog;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecommerce.Api.IntegrationTests;

// Pins HTTP behaviour that must not change when controllers become minimal endpoints.
public class HttpContractTests(PostgresFixture postgres)
{
    [Fact]
    public void T5_minimal_endpoint_json_is_strict_and_uses_string_enums()
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);

        JsonSerializerOptions json = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, json.UnmappedMemberHandling);
        Assert.Contains(json.Converters, c => c is JsonStringEnumConverter);
    }

    [Fact]
    public async Task T2_a_bad_query_value_is_a_400_problem_with_the_mvc_type_title_and_ids()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument body) = await env.GetAsync("/api/v1/products?page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("One or more validation errors occurred.", body.RootElement.GetProperty("title").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("page", out _));
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal("catalog-test-1", body.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task T3_an_unknown_product_slug_is_a_404_problem_with_the_mvc_type_title_and_ids()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument body) = await env.GetAsync("/api/v1/products/no-such-slug");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("Product not found", body.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal("catalog-test-1", body.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task T4_token_and_me_responses_carry_the_exact_no_store_headers()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("ana@example.test", UserRole.Customer);

        IdentityTestEnv.Api login = await env.LoginAsync("ana@example.test");
        string token = await env.AccessTokenAsync("ana@example.test");
        IdentityTestEnv.Api me = await env.GetAsync("/api/v1/auth/me", token);

        Assert.Equal(HttpStatusCode.OK, login.Status);
        Assert.Equal("no-store, no-cache", login.Response.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", string.Join(",", login.Response.Headers.Pragma));
        Assert.Equal(HttpStatusCode.OK, me.Status);
        Assert.Equal("no-store, no-cache", me.Response.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", string.Join(",", me.Response.Headers.Pragma));
    }
}
