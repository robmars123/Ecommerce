using System.Net;
using System.Text.Json;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-1 and I-2: the visibility rule (app context 4.2.3): inactive is invisible, even by direct link.
public class CatalogVisibilityTests(PostgresFixture postgres)
{
    private static async Task<CatalogTestEnv> SeedAsync(PostgresFixture postgres)
    {
        CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("visible-shirt", "Visible Shirt");
        env.AddProduct("inactive-shirt", "Inactive Shirt", publish: false);
        env.AddProduct("no-active-variant", "No Active Variant", variants: [new VariantSpec("NAV-1", Active: false)]);
        env.AddProduct("mixed-variants", "Mixed Variants", variants: [new VariantSpec("MIX-1", 5m, Active: false), new VariantSpec("MIX-2", 8m)]);
        await env.SaveAsync();
        return env;
    }

    [Fact]
    public async Task List_returns_only_visible_products_and_counts_only_visible_ones()
    {
        await using CatalogTestEnv env = await SeedAsync(postgres);

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, status);
        string?[] slugs = body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()).Order().ToArray();
        Assert.Equal(new[] { "mixed-variants", "visible-shirt" }, slugs);
        Assert.Equal(2, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("inactive-shirt")]
    [InlineData("no-active-variant")]
    [InlineData("does-not-exist")]
    [InlineData("Visible-Shirt")]
    public async Task Detail_is_404_problem_json_for_every_invisible_or_unknown_slug(string slug)
    {
        await using CatalogTestEnv env = await SeedAsync(postgres);

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync($"/api/v1/products/{slug}");

        Assert.Equal(HttpStatusCode.NotFound, status);
        JsonElement root = body.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        Assert.Equal("catalog-test-1", root.GetProperty("correlationId").GetString());
        Assert.False(root.TryGetProperty("variants", out _));
    }

    [Fact]
    public async Task Detail_404_is_served_as_problem_json()
    {
        await using CatalogTestEnv env = await SeedAsync(postgres);

        using HttpResponseMessage response = await env.Client.GetAsync("/api/v1/products/inactive-shirt", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Detail_of_a_visible_product_is_200()
    {
        await using CatalogTestEnv env = await SeedAsync(postgres);

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/products/visible-shirt");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("visible-shirt", body.RootElement.GetProperty("slug").GetString());
    }
}
