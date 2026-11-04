using System.Net;
using System.Text.Json;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-4: bad query input is rejected with 400 problem+json (not clamped, not a 500).
public class CatalogValidationTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=-1", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("page=-3", "page")]
    [InlineData("page=abc", "page")]
    [InlineData("pageSize=abc", "pageSize")]
    [InlineData("sort=bogus", "sort")]
    [InlineData("sort=relevance", "sort")]
    [InlineData("sort=relevance&search=%20%20", "sort")]
    public async Task Invalid_query_returns_400_problem_json_naming_the_field(string query, string field)
    {
        await using var env = await CatalogTestEnv.StartAsync(postgres);

        var (status, body) = await env.GetAsync($"/api/v1/products?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertProblem(body.RootElement, field);
    }

    [Fact]
    public async Task Search_longer_than_100_characters_returns_400()
    {
        await using var env = await CatalogTestEnv.StartAsync(postgres);

        var (status, body) = await env.GetAsync($"/api/v1/products?search={new string('a', 101)}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertProblem(body.RootElement, "search");
    }

    [Fact]
    public async Task Problem_response_content_type_is_problem_json()
    {
        await using var env = await CatalogTestEnv.StartAsync(postgres);

        using var response = await env.Client.GetAsync("/api/v1/products?pageSize=101", TestContext.Current.CancellationToken);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Boundary_values_are_accepted()
    {
        await using var env = await CatalogTestEnv.StartAsync(postgres);

        foreach (var q in new[] { "pageSize=1", "pageSize=100", "page=1", $"search={new string('a', 100)}" })
        {
            var (status, _) = await env.GetAsync($"/api/v1/products?{q}");
            Assert.Equal(HttpStatusCode.OK, status);
        }
    }

    private static void AssertProblem(JsonElement root, string field)
    {
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.GetProperty("errors").TryGetProperty(field, out _), $"errors should name '{field}': {root}");
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        Assert.Equal("catalog-test-1", root.GetProperty("correlationId").GetString());
    }
}
