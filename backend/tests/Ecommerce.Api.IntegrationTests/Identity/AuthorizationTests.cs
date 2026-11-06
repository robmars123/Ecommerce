using System.Net;
using System.Security.Claims;
using Ecommerce.Api.IntegrationTests.Catalog.Admin;
using Ecommerce.Identity.Domain;
using Ecommerce.Identity.Infrastructure.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Api.IntegrationTests.Identity;

// II-10 .. II-12: the authorization matrix, "every endpoint declares auth", and token validation.
public class AuthorizationTests(PostgresFixture postgres)
{
    private static readonly Guid SomeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly (HttpMethod Method, string Url)[] AdminEndpoints =
    [
        (HttpMethod.Get, AdminSamples.Products),
        (HttpMethod.Get, $"{AdminSamples.Products}/{SomeId}"),
        (HttpMethod.Get, $"{AdminSamples.Products}/{SomeId}/preview"),
        (HttpMethod.Post, AdminSamples.Products),
        (HttpMethod.Put, $"{AdminSamples.Products}/{SomeId}"),
        (HttpMethod.Post, $"{AdminSamples.Products}/{SomeId}/publish"),
        (HttpMethod.Post, $"{AdminSamples.Products}/{SomeId}/unpublish"),
        (HttpMethod.Post, AdminSamples.Categories),
    ];

    [Fact]
    public async Task II10_all_eight_admin_endpoints_reject_anonymous_with_401_and_customer_and_support_with_403()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("customer@example.test");
        await env.AddUserAsync("support@example.test", UserRole.Support);
        string customer = await env.AccessTokenAsync("customer@example.test");
        string support = await env.AccessTokenAsync("support@example.test");

        foreach ((HttpMethod? method, string? url) in AdminEndpoints)
        {
            object? body = method == HttpMethod.Get ? null : new { };
            AssertProblem(await env.SendAsync(method, url, body), HttpStatusCode.Unauthorized, $"anon {method} {url}");
            AssertProblem(await env.SendAsync(method, url, body, customer), HttpStatusCode.Forbidden, $"customer {method} {url}");
            AssertProblem(await env.SendAsync(method, url, body, support), HttpStatusCode.Forbidden, $"support {method} {url}");
        }
    }

    [Fact]
    public async Task II10_an_admin_gets_2xx_on_each_admin_endpoint_and_me_returns_only_the_callers_data()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        string admin = await AdminSamples.AdminTokenAsync(env);
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, admin); // POST products + POST categories (201)
        string url = $"{AdminSamples.Products}/{created.Str("id")}";

        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync(AdminSamples.Products, admin)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync(url, admin)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync(url + "/preview", admin)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.SendAsync(HttpMethod.Put, url, AdminSamples.UpdateBody(created.Json, name: "Renamed"), admin)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.PostAsync(url + "/publish", null, admin)).Status);
        Assert.Equal(HttpStatusCode.OK, (await env.PostAsync(url + "/unpublish", null, admin)).Status);

        await env.AddUserAsync("ana@example.test");
        IdentityTestEnv.Api me = await env.GetAsync("/api/v1/auth/me", await env.AccessTokenAsync("ana@example.test"));
        Assert.Equal(HttpStatusCode.OK, me.Status);
        Assert.Equal("ana@example.test", me.Str("email"));
        Assert.Equal("Customer", me.Str("role"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await env.GetAsync("/api/v1/auth/me")).Status);
    }

    private static void AssertProblem(IdentityTestEnv.Api r, HttpStatusCode expected, string what)
    {
        Assert.True(expected == r.Status, $"{what}: expected {expected}, got {r.Status}");
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrEmpty(r.Str("traceId")), what);
        Assert.Equal("id-test-1", r.Str("correlationId"));
    }

    // ---- II-11: every endpoint declares its auth; the anonymous set is exactly the expected list ----

    internal sealed record EndpointAuth(string Route, bool DeclaresAuth, bool Anonymous);

    /// <summary>Pure rule: returns the endpoints that declare nothing, and the anonymous routes that are not on the list.</summary>
    internal static IReadOnlyList<string> Violations(IEnumerable<EndpointAuth> endpoints, IReadOnlySet<string> expectedAnonymous) =>
        endpoints.Where(e => !e.DeclaresAuth).Select(e => $"undeclared: {e.Route}")
            .Concat(endpoints.Where(e => e.Anonymous && !expectedAnonymous.Contains(e.Route)).Select(e => $"unexpected anonymous: {e.Route}"))
            .Concat(expectedAnonymous.Where(r => endpoints.All(e => !(e.Anonymous && e.Route == r))).Select(r => $"missing anonymous: {r}"))
            .ToList();

    private static readonly HashSet<string> ExpectedAnonymous =
    [
        "api/v1/products", "api/v1/products/{slug}", "api/v1/categories",
        "health/live", "health/ready",
        "api/v1/auth/register", "api/v1/auth/login", "api/v1/auth/refresh", "api/v1/auth/logout",
        "api/v1/auth/password-reset", "api/v1/auth/password-reset/confirm",
        "{*path:nonfile}", // the 404 terminal endpoint
    ];

    [Fact]
    public void II11_self_test_the_rule_flags_an_undeclared_and_an_unexpected_anonymous_endpoint()
    {
        HashSet<string> set = new HashSet<string> { "a" };
        Assert.Contains("undeclared: b", Violations([new("a", true, true), new("b", false, false)], set));
        Assert.Contains("unexpected anonymous: c", Violations([new("a", true, true), new("c", true, true)], set));
        Assert.Contains("missing anonymous: a", Violations([new("z", true, false)], set));
        Assert.Empty(Violations([new("a", true, true), new("z", true, false)], set));
    }

    [Fact]
    public void II11_every_endpoint_declares_auth_and_the_anonymous_set_is_exactly_the_public_list()
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);
        List<EndpointAuth> endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new EndpointAuth(
                e.RoutePattern.RawText!.TrimStart('/'),
                e.Metadata.GetMetadata<IAllowAnonymous>() is not null || e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0,
                e.Metadata.GetMetadata<IAllowAnonymous>() is not null))
            .ToList();

        Assert.True(endpoints.Count > 15);
        Assert.Empty(Violations(endpoints, ExpectedAnonymous));
    }

    // ---- II-12: bad tokens are 401 ----

    private static string Mint(
        SecurityKey key,
        Guid userId,
        string role = "Customer",
        string alg = SecurityAlgorithms.HmacSha256,
        string issuer = "ecommerce-api",
        string audience = "ecommerce-store",
        TimeSpan? expiresIn = null)
    {
        DateTime now = DateTime.UtcNow;
        DateTime expires = now + (expiresIn ?? TimeSpan.FromMinutes(10));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("role", role)]),
            Issuer = issuer,
            Audience = audience,
            NotBefore = expires < now ? expires.AddHours(-1) : now.AddMinutes(-1),
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, alg),
        });
    }

    [Fact]
    public async Task II12_forged_expired_unsigned_and_wrong_issuer_or_audience_tokens_are_401_and_a_good_one_is_200()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        User user = await env.AddUserAsync("ana@example.test");
        SymmetricSecurityKey key = env.Factory.Services.GetRequiredService<JwtSigningKeyProvider>().Key;
        SymmetricSecurityKey otherKey = new SymmetricSecurityKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        string B64(string json) => Base64UrlEncoder.Encode(json);
        string none = $"{B64("""{"alg":"none","typ":"JWT"}""")}.{B64($$"""{"sub":"{{user.Id}}","role":"Admin","iss":"ecommerce-api","aud":"ecommerce-store","exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}""")}.";

        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync("/api/v1/auth/me", Mint(key, user.Id))).Status);
        foreach ((string? name, string? token) in new (string, string)[]
        {
            ("other key", Mint(otherKey, user.Id)),
            ("expired", Mint(key, user.Id, expiresIn: TimeSpan.FromMinutes(-5))),
            ("alg none", none),
            ("wrong issuer", Mint(key, user.Id, issuer: "someone-else")),
            ("wrong audience", Mint(key, user.Id, audience: "someone-else")),
            ("garbage", "not.a.jwt"),
        })
        {
            IdentityTestEnv.Api r = await env.GetAsync("/api/v1/auth/me", token);
            Assert.True(r.Status == HttpStatusCode.Unauthorized, $"{name}: expected 401, got {r.Status}");
        }
    }
}
