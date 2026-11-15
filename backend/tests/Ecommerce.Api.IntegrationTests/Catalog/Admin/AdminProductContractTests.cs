using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests.Catalog.Admin;

// IP1 .. IP7: the HTTP shape of the admin product endpoints (status, problem body, upload route conventions).
public class AdminProductContractTests(PostgresFixture postgres)
{
    private const string TypeBadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string TypeNotFound = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string TypeConflict = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string UploadRoute = "POST api/v1/admin/products/{id:guid}/images";

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
    }

    private static string Url(IdentityTestEnv.Api product) => $"{AdminSamples.Products}/{product.Str("id")}";

    [Fact]
    public async Task IP1_a_stale_update_is_a_409_problem_with_the_version_error()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);
        await env.SendAsync(HttpMethod.Put, Url(created), AdminSamples.UpdateBody(created.Json, name: "First"), token);

        IdentityTestEnv.Api stale = await env.SendAsync(HttpMethod.Put, Url(created), AdminSamples.UpdateBody(created.Json, name: "Second"), token);

        AssertProblem(stale, HttpStatusCode.Conflict, TypeConflict);
        Assert.Equal("Conflict", stale.Str("title"));
        Assert.False(string.IsNullOrEmpty(stale.Json.GetProperty("errors").GetProperty("version")[0].GetString()));
    }

    [Fact]
    public async Task IP2_an_unknown_product_is_a_404_problem_on_every_id_route()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);
        string unknown = $"{AdminSamples.Products}/{Guid.NewGuid()}";

        IdentityTestEnv.Api[] results =
        [
            await env.GetAsync(unknown, token),
            await env.GetAsync(unknown + "/preview", token),
            await env.SendAsync(HttpMethod.Put, unknown, AdminSamples.UpdateBody(created.Json), token),
            await env.PostAsync(unknown + "/publish", null, token),
            await env.PostAsync(unknown + "/unpublish", null, token),
        ];

        foreach (IdentityTestEnv.Api r in results)
        {
            AssertProblem(r, HttpStatusCode.NotFound, TypeNotFound);
            Assert.Equal("Product not found", r.Str("title"));
        }
    }

    [Fact]
    public async Task IP3_a_bad_price_is_a_400_with_the_camelCase_key_on_create_and_update()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);
        string updateBody = JsonSerializer.Serialize(AdminSamples.UpdateBody(created.Json)).Replace("\"amount\":\"49.00\"", "\"amount\":\"abc\"");

        IdentityTestEnv.Api create = await env.PostAsync(AdminSamples.Products, AdminSamples.Product("other", "O-1", variants: [AdminSamples.Variant("O-1", amount: "abc")]), token);
        IdentityTestEnv.Api update = await env.SendAsync(HttpMethod.Put, Url(created), updateBody, token);

        foreach (IdentityTestEnv.Api r in new[] { create, update })
        {
            AssertProblem(r, HttpStatusCode.BadRequest, TypeBadRequest);
            Assert.Equal("One or more validation errors occurred.", r.Str("title"));
            Assert.True(r.Json.GetProperty("errors").TryGetProperty("variants[0].price.amount", out _));
        }
    }

    [Fact]
    public async Task IP4_a_body_without_its_lists_is_a_400_with_errors()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);

        IdentityTestEnv.Api create = await env.SendAsync(HttpMethod.Post, AdminSamples.Products,
            "{\"slug\":\"x\",\"name\":\"X\",\"description\":\"d\",\"categorySlug\":\"shirts\",\"images\":[]}", token);
        IdentityTestEnv.Api update = await env.SendAsync(HttpMethod.Put, Url(created),
            "{\"version\":1,\"slug\":\"x\",\"name\":\"X\",\"description\":\"d\",\"categorySlug\":\"shirts\",\"variants\":[]}", token);

        foreach (IdentityTestEnv.Api r in new[] { create, update })
        {
            Assert.Equal(HttpStatusCode.BadRequest, r.Status);
            Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
            Assert.True(r.Json.TryGetProperty("errors", out _));
        }
    }

    [Fact]
    public async Task IP5_list_query_parses_in_two_stages_and_reads_the_first_repeated_value()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api a = await AdminSamples.CreateProductAsync(env, token, AdminSamples.Product("alpha", "A-1"));
        await AdminSamples.CreateProductAsync(env, token, AdminSamples.Product("beta", "B-1"));
        await env.SendAsync(HttpMethod.Put, Url(a), AdminSamples.UpdateBody(a.Json, variantActive: true), token);
        await env.PostAsync(Url(a) + "/publish", null, token);

        IdentityTestEnv.Api badPage = await env.GetAsync(AdminSamples.Products + "?page=abc", token);
        IdentityTestEnv.Api parseHidesRange = await env.GetAsync(AdminSamples.Products + "?page=abc&pageSize=500", token);
        IdentityTestEnv.Api firstStatus = await env.GetAsync(AdminSamples.Products + "?status=active&status=bogus", token);
        IdentityTestEnv.Api firstPage = await env.GetAsync(AdminSamples.Products + "?page=1&page=abc", token);

        Assert.Equal(HttpStatusCode.BadRequest, badPage.Status);
        Assert.Equal("The value 'abc' is not valid.", badPage.Json.GetProperty("errors").GetProperty("page")[0].GetString());
        Assert.Equal(HttpStatusCode.BadRequest, parseHidesRange.Status);
        Assert.True(parseHidesRange.Json.GetProperty("errors").TryGetProperty("page", out _));
        Assert.False(parseHidesRange.Json.GetProperty("errors").TryGetProperty("pageSize", out _));
        Assert.Equal(HttpStatusCode.OK, firstStatus.Status);
        Assert.Equal(["alpha"], firstStatus.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!).ToArray());
        Assert.Equal(HttpStatusCode.OK, firstPage.Status);
    }

    [Fact]
    public void IP6_the_upload_route_carries_the_size_limits_the_form_limit_and_the_multipart_only_accepts()
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);

        RouteEndpoint endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single()} {e.RoutePattern.RawText!.TrimStart('/')}" == UploadRoute);

        Assert.Equal(5308416, endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize);
        Assert.Equal(5242881, endpoint.Metadata.GetMetadata<IFormOptionsMetadata>()?.MultipartBodyLengthLimit);
        IAcceptsMetadata? accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
        Assert.Equal(["multipart/form-data"], accepts?.ContentTypes);
        Assert.False(accepts!.IsOptional);
        Assert.NotEqual(true, endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation);
    }

    [Fact]
    public async Task IP7_an_upload_with_no_content_type_is_answered_like_today_and_stores_nothing()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api product = await AdminSamples.CreateProductAsync(env, token);
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{Url(product)}/images");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await env.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        int blobs = await env.Db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM image_blobs").SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, blobs);
    }
}
