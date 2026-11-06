using System.Net;
using System.Text.Json;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-5, I-6, I-7, I-8.
public class CatalogSearchTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Search_matches_by_stemming_in_name_and_by_description_text()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("blue-shirt", "Blue Shirt");
        env.AddProduct("garden-hose", "Garden Hose", description: "Durable rubber tubing for the garden");
        env.AddProduct("red-mug", "Red Mug");
        await env.SaveAsync();

        Assert.Equal(["blue-shirt"], await Slugs(env, "search=shirts"));
        Assert.Equal(["garden-hose"], await Slugs(env, "search=tubing"));
        Assert.Equal(["blue-shirt"], await Slugs(env, "search=%22blue%20shirt%22"));
    }

    [Fact]
    public async Task Search_with_no_match_returns_an_empty_page()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("blue-shirt", "Blue Shirt");
        await env.SaveAsync();

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/products?search=zeppelin");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("a%20%26%20b%20%7C%20!c%20%3A*%20(%27")]
    [InlineData("%22unterminated")]
    [InlineData("the")]
    [InlineData("%27%3B%20DROP%20TABLE%20products%3B--")]
    [InlineData("%3A*")]
    [InlineData("%5C")]
    public async Task Hostile_search_input_never_causes_a_server_error(string encoded)
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("blue-shirt", "Blue Shirt");
        await env.SaveAsync();

        (HttpStatusCode status, JsonDocument _) = await env.GetAsync($"/api/v1/products?search={encoded}");

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task A_name_match_ranks_above_a_description_only_match_even_when_the_latter_is_newer()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("desc-only", "Garden Hose", description: "Sold with a free umbrella", createdAt: CatalogTestEnv.Epoch.AddDays(2));
        env.AddProduct("name-match", "Umbrella", description: "Keeps you dry", createdAt: CatalogTestEnv.Epoch);
        await env.SaveAsync();

        Assert.Equal(["name-match", "desc-only"], await Slugs(env, "search=umbrella"));
    }

    [Fact]
    public async Task Category_filter_by_parent_includes_children_and_by_child_is_exact_and_unknown_is_empty()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddCategory("clothing", "Clothing");
        env.AddCategory("tops", "Tops", "clothing");
        env.AddCategory("bottoms", "Bottoms", "clothing");
        env.AddCategory("home", "Home");
        env.AddCategory("kitchen", "Kitchen", "home");
        env.AddProduct("shirt", "Shirt", category: "tops");
        env.AddProduct("jeans", "Jeans", category: "bottoms");
        env.AddProduct("gift-card", "Gift Card", category: "clothing");
        env.AddProduct("mug", "Mug", category: "kitchen");
        await env.SaveAsync();

        Assert.Equal(["gift-card", "jeans", "shirt"], (await Slugs(env, "category=clothing")).Order().ToArray());
        Assert.Equal(["shirt"], await Slugs(env, "category=tops"));
        Assert.Equal(["mug"], await Slugs(env, "category=kitchen"));
        Assert.Equal(["gift-card"], await Slugs(env, "category=clothing&search=gift"));

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/products?category=no-such-category");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Price_sorts_use_the_minimum_active_variant_price_and_ignore_cheaper_inactive_variants()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("a", "A", variants: [new VariantSpec("A-1", 20m), new VariantSpec("A-0", 5m, Active: false)]);
        env.AddProduct("b", "B", variants: [new VariantSpec("B-1", 30m), new VariantSpec("B-2", 10m, CompareAt: 12m)]);
        env.AddProduct("c", "C", variants: [new VariantSpec("C-1", 15m)]);
        await env.SaveAsync();

        Assert.Equal(["b", "c", "a"], await Slugs(env, "sort=price-asc"));
        Assert.Equal(["a", "c", "b"], await Slugs(env, "sort=price-desc"));

        (HttpStatusCode _, JsonDocument? body) = await env.GetAsync("/api/v1/products?sort=price-asc");
        JsonElement first = body.RootElement.GetProperty("items")[0];
        Assert.Equal("10.00", first.GetProperty("price").GetProperty("amount").GetString());
        Assert.Equal("12.00", first.GetProperty("compareAtPrice").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task Newest_sort_orders_by_created_at_descending_and_is_the_default_without_search()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("old", "Old", createdAt: CatalogTestEnv.Epoch);
        env.AddProduct("new", "New", createdAt: CatalogTestEnv.Epoch.AddDays(3));
        env.AddProduct("mid", "Mid", createdAt: CatalogTestEnv.Epoch.AddDays(1));
        await env.SaveAsync();

        Assert.Equal(["new", "mid", "old"], await Slugs(env, "sort=newest"));
        Assert.Equal(["new", "mid", "old"], await Slugs(env, ""));
    }

    private static async Task<string[]> Slugs(CatalogTestEnv env, string query)
    {
        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync($"/api/v1/products?{query}");
        Assert.Equal(HttpStatusCode.OK, status);
        return CatalogTestEnv.ItemSlugs(body);
    }
}
