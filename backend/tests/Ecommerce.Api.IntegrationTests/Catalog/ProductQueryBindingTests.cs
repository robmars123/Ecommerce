using System.Net;
using System.Text.Json;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// P2.60: pins how the products endpoints read page and pageSize, so the transport can change without drift.
public class ProductQueryBindingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task B1_empty_page_and_pageSize_use_the_defaults()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument body) = await env.GetAsync("/api/v1/products?page=&pageSize=");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, body.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task B1b_whitespace_page_and_pageSize_use_the_defaults()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument body) = await env.GetAsync("/api/v1/products?page=%20&pageSize=%20");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, body.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task B2_non_numeric_page_returns_the_expected_message()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument body) = await env.GetAsync("/api/v1/products?page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("The value 'abc' is not valid.", body.RootElement.GetProperty("errors").GetProperty("page")[0].GetString());
    }

    [Fact]
    public async Task B3_a_parse_error_on_page_hides_the_range_error_on_pageSize()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument body) = await env.GetAsync("/api/v1/products?page=abc&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        JsonElement errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("page", out _));
        Assert.False(errors.TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task B4_page_with_surrounding_spaces_is_accepted()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);

        (HttpStatusCode status, JsonDocument body) = await env.GetAsync("/api/v1/products?page=%202%20");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, body.RootElement.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task B5_list_and_detail_responses_have_no_cache_control_header()
    {
        await using CatalogTestEnv env = await CatalogTestEnv.StartAsync(postgres);
        env.AddProduct("tee", "Tee");
        await env.SaveAsync();

        foreach (string url in new[] { "/api/v1/products", "/api/v1/products/tee" })
        {
            using HttpResponseMessage response = await env.Client.GetAsync(url, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null(response.Headers.CacheControl);
        }
    }
}
