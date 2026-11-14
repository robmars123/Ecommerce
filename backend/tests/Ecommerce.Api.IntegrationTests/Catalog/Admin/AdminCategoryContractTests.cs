using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Catalog.Admin;

// IC11 .. IC15: the HTTP shape of the admin category endpoints (status, headers, problem body).
public class AdminCategoryContractTests(PostgresFixture postgres)
{
    private const string TypeBadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string TypeNotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string TypeConflict = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private static async Task<(IdentityTestEnv Env, string Token)> StartAsync(PostgresFixture postgres)
    {
        IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        return (env, await AdminSamples.AdminTokenAsync(env));
    }

    private static void AssertProblem(IdentityTestEnv.Api r, HttpStatusCode status, string type)
    {
        Assert.Equal(status, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(type, r.Str("type"));
        Assert.False(string.IsNullOrEmpty(r.Str("traceId")));
        Assert.Equal("id-test-1", r.Str("correlationId"));
    }

    [Fact]
    public async Task IC11_create_is_201_with_no_location_and_the_exact_body()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;

        IdentityTestEnv.Api tops = await env.PostAsync(AdminSamples.Categories, new { slug = "tops", name = "Tops", parentSlug = (string?)null }, token);
        IdentityTestEnv.Api tees = await env.PostAsync(AdminSamples.Categories, new { slug = "tees", name = "Tees", parentSlug = "tops" }, token);

        Assert.Equal(HttpStatusCode.Created, tops.Status);
        Assert.Null(tops.Response.Headers.Location);
        Assert.Equal("application/json", tops.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("tops", tops.Str("slug"));
        Assert.Equal("Tops", tops.Str("name"));
        Assert.Equal(JsonValueKind.Null, tops.Json.GetProperty("parentSlug").ValueKind);
        Assert.Equal(HttpStatusCode.Created, tees.Status);
        Assert.Null(tees.Response.Headers.Location);
        Assert.Equal("tees", tees.Str("slug"));
        Assert.Equal("Tees", tees.Str("name"));
        Assert.Equal("tops", tees.Str("parentSlug"));
    }

    [Fact]
    public async Task IC12_unreadable_json_is_a_400_problem_and_writes_nothing()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, token, "shirts");

        IdentityTestEnv.Api unknownMember = await env.SendAsync(HttpMethod.Post, AdminSamples.Categories, "{\"slug\":\"a\",\"name\":\"A\",\"parentSlug\":null,\"isActive\":true}", token);
        IdentityTestEnv.Api putUnknown = await env.SendAsync(HttpMethod.Put, AdminSamples.Categories + "/shirts", "{\"slug\":\"b\",\"name\":\"B\",\"parentSlug\":\"x\"}", token);
        IdentityTestEnv.Api malformed = await env.SendAsync(HttpMethod.Post, AdminSamples.Categories, "{\"slug\":", token);
        IdentityTestEnv.Api empty = await env.SendAsync(HttpMethod.Post, AdminSamples.Categories, "", token);

        AssertProblem(unknownMember, HttpStatusCode.BadRequest, TypeBadRequest);
        AssertProblem(putUnknown, HttpStatusCode.BadRequest, TypeBadRequest);
        AssertProblem(malformed, HttpStatusCode.BadRequest, TypeBadRequest);
        AssertProblem(empty, HttpStatusCode.BadRequest, TypeBadRequest);
        string cats = (await env.GetAsync("/api/v1/categories")).Text;
        Assert.Contains("shirts", cats);
        Assert.DoesNotContain("\"a\"", cats);
        Assert.DoesNotContain("\"b\"", cats);
    }

    [Fact]
    public async Task IC13_validation_400_has_the_title_and_the_error_keys()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;

        IdentityTestEnv.Api bad = await env.PostAsync(AdminSamples.Categories, new { slug = "Bad Slug", name = "" }, token);
        IdentityTestEnv.Api omitted = await env.SendAsync(HttpMethod.Post, AdminSamples.Categories, "{\"slug\":\"a\"}", token);
        IdentityTestEnv.Api parent = await env.PostAsync(AdminSamples.Categories, new { slug = "a", name = "A", parentSlug = "missing" }, token);

        AssertProblem(bad, HttpStatusCode.BadRequest, TypeBadRequest);
        Assert.Equal("One or more validation errors occurred.", bad.Str("title"));
        Assert.True(bad.Json.GetProperty("errors").TryGetProperty("slug", out _));
        Assert.True(bad.Json.GetProperty("errors").TryGetProperty("name", out _));
        AssertProblem(omitted, HttpStatusCode.BadRequest, TypeBadRequest);
        Assert.Equal("One or more validation errors occurred.", omitted.Str("title"));
        Assert.Contains(omitted.Json.GetProperty("errors").EnumerateObject(), p => string.Equals(p.Name, "name", StringComparison.OrdinalIgnoreCase));
        AssertProblem(parent, HttpStatusCode.BadRequest, TypeBadRequest);
        Assert.Equal("One or more validation errors occurred.", parent.Str("title"));
        Assert.Equal("That parent category does not exist.", parent.Json.GetProperty("errors").GetProperty("parentSlug")[0].GetString());
    }

    [Fact]
    public async Task IC14_put_and_delete_on_an_unknown_slug_are_404_problems()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;

        IdentityTestEnv.Api put = await env.SendAsync(HttpMethod.Put, AdminSamples.Categories + "/nope", new { slug = "tops", name = "Tops" }, token);
        IdentityTestEnv.Api delete = await env.SendAsync(HttpMethod.Delete, AdminSamples.Categories + "/nope", null, token);

        AssertProblem(put, HttpStatusCode.NotFound, TypeNotFound);
        Assert.Equal("Category not found", put.Str("title"));
        AssertProblem(delete, HttpStatusCode.NotFound, TypeNotFound);
        Assert.Equal("Category not found", delete.Str("title"));
    }

    [Fact]
    public async Task IC15_delete_of_a_parent_with_a_child_is_a_409_problem()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateCategoryAsync(env, token, "tops");
        await AdminSamples.CreateCategoryAsync(env, token, "tees", "tops");

        IdentityTestEnv.Api r = await env.SendAsync(HttpMethod.Delete, AdminSamples.Categories + "/tops", null, token);

        AssertProblem(r, HttpStatusCode.Conflict, TypeConflict);
        Assert.Equal("Conflict", r.Str("title"));
        Assert.Equal("This category has subcategories. Delete them first.", r.Json.GetProperty("errors").GetProperty("subcategories")[0].GetString());
    }
}
