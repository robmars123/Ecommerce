using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Api.IntegrationTests.Catalog.Admin;

// IC1 .. IC9: admin category edit and delete over HTTP with a real Postgres (IC10 is IA7, unchanged).
public class AdminCategoryApiTests(PostgresFixture postgres)
{
    private static string Url(string slug) => $"{AdminSamples.Categories}/{slug}";

    private static async Task<(IdentityTestEnv Env, string Token)> StartAsync(PostgresFixture postgres)
    {
        IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        return (env, await AdminSamples.AdminTokenAsync(env));
    }

    private static bool HasError(IdentityTestEnv.Api r, string key) => r.Json.GetProperty("errors").TryGetProperty(key, out _);

    private static async Task<string> CategoriesTextAsync(IdentityTestEnv env) => (await env.GetAsync("/api/v1/categories")).Text;

    [Fact]
    public async Task IC1_put_renames_slug_and_name_and_old_slug_lists_nothing()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);
        string productUrl = $"{AdminSamples.Products}/{created.Str("id")}";
        Assert.Equal(HttpStatusCode.OK, (await env.SendAsync(HttpMethod.Put, productUrl, AdminSamples.UpdateBody(created.Json, variantActive: true), token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.PostAsync(productUrl + "/publish", null, token)).Status);

        IdentityTestEnv.Api r = await env.SendAsync(HttpMethod.Put, Url("shirts"), new { slug = "tops", name = "Tops" }, token);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal("tops", r.Str("slug"));
        Assert.Equal("Tops", r.Str("name"));
        Assert.Equal(JsonValueKind.Null, r.Json.GetProperty("parentSlug").ValueKind);
        string cats = await CategoriesTextAsync(env);
        Assert.Contains("tops", cats);
        Assert.DoesNotContain("shirts", cats);
        Assert.Single((await env.GetAsync("/api/v1/products?category=tops")).Json.GetProperty("items").EnumerateArray());
        IdentityTestEnv.Api old = await env.GetAsync("/api/v1/products?category=shirts");
        Assert.Equal(HttpStatusCode.OK, old.Status);
        Assert.Empty(old.Json.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task IC2_put_on_a_child_keeps_its_parent()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, token, "shirts");
        await AdminSamples.CreateCategoryAsync(env, token, "linen", "shirts");

        IdentityTestEnv.Api r = await env.SendAsync(HttpMethod.Put, Url("linen"), new { slug = "flax", name = "Flax" }, token);

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal("shirts", r.Str("parentSlug"));
        Assert.Contains("shirts", await CategoriesTextAsync(env));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("Support", HttpStatusCode.Forbidden)]
    public async Task IC3_put_and_delete_need_an_admin(string? role, HttpStatusCode expected)
    {
        (IdentityTestEnv? env, string? adminToken) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, adminToken);
        string? token = null;
        if (role is not null)
        {
            await env.AddUserAsync("other@example.test", Enum.Parse<UserRole>(role));
            token = await env.AccessTokenAsync("other@example.test");
        }

        Assert.Equal(expected, (await env.SendAsync(HttpMethod.Put, Url("shirts"), new { slug = "tops", name = "Tops" }, token)).Status);
        Assert.Equal(expected, (await env.SendAsync(HttpMethod.Delete, Url("shirts"), null, token)).Status);

        Assert.Contains("shirts", await CategoriesTextAsync(env));
    }

    [Fact]
    public async Task IC4_put_rejects_bad_slug_blank_or_long_name_and_a_parentSlug_member()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, token);

        IdentityTestEnv.Api badSlug = await env.SendAsync(HttpMethod.Put, Url("shirts"), new { slug = "Bad Slug", name = "X" }, token);
        IdentityTestEnv.Api blank = await env.SendAsync(HttpMethod.Put, Url("shirts"), new { slug = "tops", name = "" }, token);
        IdentityTestEnv.Api longName = await env.SendAsync(HttpMethod.Put, Url("shirts"), new { slug = "tops", name = new string('n', 101) }, token);
        IdentityTestEnv.Api parent = await env.SendAsync(HttpMethod.Put, Url("shirts"), new { slug = "tops", name = "Tops", parentSlug = "x" }, token);

        Assert.Equal(HttpStatusCode.BadRequest, badSlug.Status);
        Assert.True(HasError(badSlug, "slug"));
        Assert.Equal(HttpStatusCode.BadRequest, blank.Status);
        Assert.True(HasError(blank, "name"));
        Assert.Equal(HttpStatusCode.BadRequest, longName.Status);
        Assert.True(HasError(longName, "name"));
        Assert.Equal(HttpStatusCode.BadRequest, parent.Status);
        string cats = await CategoriesTextAsync(env);
        Assert.Contains("shirts", cats);
        Assert.DoesNotContain("tops", cats);
    }

    [Fact]
    public async Task IC5_put_and_delete_on_an_unknown_slug_are_404()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;

        Assert.Equal(HttpStatusCode.NotFound, (await env.SendAsync(HttpMethod.Put, Url("nope"), new { slug = "tops", name = "Tops" }, token)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await env.SendAsync(HttpMethod.Delete, Url("nope"), null, token)).Status);
    }

    [Fact]
    public async Task IC6_put_to_a_slug_another_category_has_is_409_on_slug()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, token, "shirts");
        await AdminSamples.CreateCategoryAsync(env, token, "pants");

        IdentityTestEnv.Api r = await env.SendAsync(HttpMethod.Put, Url("pants"), new { slug = "shirts", name = "Pants" }, token);

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.True(HasError(r, "slug"));
        string cats = await CategoriesTextAsync(env);
        Assert.Contains("shirts", cats);
        Assert.Contains("pants", cats);
    }

    [Fact]
    public async Task IC7_delete_of_an_empty_category_is_204_then_404()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, token);

        Assert.Equal(HttpStatusCode.NoContent, (await env.SendAsync(HttpMethod.Delete, Url("shirts"), null, token)).Status);

        Assert.DoesNotContain("shirts", await CategoriesTextAsync(env));
        Assert.Equal(HttpStatusCode.NotFound, (await env.SendAsync(HttpMethod.Delete, Url("shirts"), null, token)).Status);
    }

    [Fact]
    public async Task IC8_delete_is_409_while_even_an_inactive_product_uses_the_category()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api product = await AdminSamples.CreateProductAsync(env, token);
        Assert.False(product.Json.GetProperty("isActive").GetBoolean());

        IdentityTestEnv.Api r = await env.SendAsync(HttpMethod.Delete, Url("shirts"), null, token);

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.True(HasError(r, "products"));
        Assert.Contains("shirts", await CategoriesTextAsync(env));
        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync($"{AdminSamples.Products}/{product.Str("id")}", token)).Status);
    }

    [Fact]
    public async Task IC9_delete_is_409_while_a_subcategory_exists_then_child_first_succeeds()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, token, "shirts");
        await AdminSamples.CreateCategoryAsync(env, token, "linen", "shirts");

        IdentityTestEnv.Api r = await env.SendAsync(HttpMethod.Delete, Url("shirts"), null, token);

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.True(HasError(r, "subcategories"));
        Assert.Equal(HttpStatusCode.NoContent, (await env.SendAsync(HttpMethod.Delete, Url("linen"), null, token)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await env.SendAsync(HttpMethod.Delete, Url("shirts"), null, token)).Status);
    }
}
