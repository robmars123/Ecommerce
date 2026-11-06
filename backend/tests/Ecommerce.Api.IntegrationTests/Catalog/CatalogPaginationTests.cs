using System.Net;
using System.Text.Json;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-9: offset pagination is stable only if every sort ends in a unique tie-breaker.
public class CatalogPaginationTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("newest", 10)]
    [InlineData("price-asc", 10)]
    [InlineData("price-desc", 10)]
    [InlineData("newest", 3)]
    [InlineData("price-asc", 3)]
    [InlineData("price-desc", 3)]
    public async Task Pages_cover_all_rows_exactly_once_when_price_and_created_at_are_identical(string sort, int pageSize)
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        for (int i = 0; i < 25; i++)
        {
            env.AddProduct($"item-{i:00}", $"Item {i:00}", variants: [new VariantSpec($"ITEM-{i:00}", 10m)]);
        }
        await env.SaveAsync();

        List<string> seen = new List<string>();
        int pages = (int)Math.Ceiling(25 / (double)pageSize);
        for (int page = 1; page <= pages; page++)
        {
            (HttpStatusCode status, JsonDocument? body) = await env.GetAsync($"/api/v1/products?sort={sort}&page={page}&pageSize={pageSize}");
            Assert.Equal(HttpStatusCode.OK, status);
            seen.AddRange(body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!));
        }

        Assert.Equal(25, seen.Count);
        Assert.Equal(25, seen.Distinct().Count());
    }

    [Theory]
    [InlineData(10)]
    [InlineData(3)]
    public async Task Relevance_pages_cover_all_rows_exactly_once_when_rank_and_created_at_are_identical(int pageSize)
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        for (int i = 0; i < 25; i++)
        {
            env.AddProduct($"item-{i:00}", "Cotton Shirt", description: "Soft cotton everyday shirt",
                variants: [new VariantSpec($"ITEM-{i:00}", 10m)]);
        }
        await env.SaveAsync();

        List<string> seen = new List<string>();
        int pages = (int)Math.Ceiling(25 / (double)pageSize);
        for (int page = 1; page <= pages; page++)
        {
            (HttpStatusCode status, JsonDocument? body) = await env.GetAsync($"/api/v1/products?search=cotton&sort=relevance&page={page}&pageSize={pageSize}");
            Assert.Equal(HttpStatusCode.OK, status);
            seen.AddRange(body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!));
        }

        Assert.Equal(25, seen.Count);
        Assert.Equal(25, seen.Distinct().Count());
    }

    [Fact]
    public async Task Page_past_the_end_is_200_with_empty_items_and_the_real_totals()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        for (int i = 0; i < 25; i++)
        {
            env.AddProduct($"item-{i:00}", $"Item {i:00}");
        }
        await env.SaveAsync();

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/products?page=4&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, status);
        JsonElement root = body.RootElement;
        Assert.Empty(root.GetProperty("items").EnumerateArray());
        Assert.Equal(25, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, root.GetProperty("totalPages").GetInt32());
        Assert.Equal(4, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Empty_catalog_has_zero_total_pages()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument? body) = await env.GetAsync("/api/v1/products");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal(0, body.RootElement.GetProperty("totalCount").GetInt32());
    }
}
