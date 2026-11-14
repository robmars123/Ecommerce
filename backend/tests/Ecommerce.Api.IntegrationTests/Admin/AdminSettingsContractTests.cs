using System.Net;
using Ecommerce.Identity.Domain;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Admin;

// P2.62: pins the settings route's auth problem shape and credentialed CORS, so they survive the move to Minimal API.
public class AdminSettingsContractTests(PostgresFixture postgres)
{
    private const string Allowed = "https://store.test.invalid";
    private const string Url = "/api/v1/admin/settings";

    [Fact]
    public async Task Settings_401_and_Support_403_are_problem_json()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await env.AddUserAsync("support@example.test", UserRole.Support);
        string support = await env.AccessTokenAsync("support@example.test");

        AssertProblem(await env.SendAsync(HttpMethod.Get, Url, null), HttpStatusCode.Unauthorized, "anonymous");
        AssertProblem(await env.SendAsync(HttpMethod.Get, Url, null, support), HttpStatusCode.Forbidden, "support");
    }

    [Fact]
    public async Task Settings_cors_is_credentialed_for_the_configured_origin_only()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable, settings: new Dictionary<string, string> { ["Cors:AllowedOrigins:0"] = Allowed });
        using HttpClient client = factory.CreateClient();

        using HttpRequestMessage preflight = new HttpRequestMessage(HttpMethod.Options, Url);
        preflight.Headers.Add("Origin", Allowed);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");
        HttpResponseMessage allowed = await client.SendAsync(preflight, ct);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(Allowed, Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Credentials")));

        using HttpRequestMessage other = new HttpRequestMessage(HttpMethod.Options, Url);
        other.Headers.Add("Origin", "https://evil.test.invalid");
        other.Headers.Add("Access-Control-Request-Method", "GET");
        Assert.False((await client.SendAsync(other, ct)).Headers.Contains("Access-Control-Allow-Origin"));

        // The web app must be able to read a 401 (to refresh), so the CORS headers ride on it too.
        using HttpRequestMessage anonymous = new HttpRequestMessage(HttpMethod.Get, Url);
        anonymous.Headers.Add("Origin", Allowed);
        HttpResponseMessage unauthorized = await client.SendAsync(anonymous, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(Allowed, Assert.Single(unauthorized.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(unauthorized.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    private static void AssertProblem(IdentityTestEnv.Api r, HttpStatusCode expected, string what)
    {
        Assert.True(expected == r.Status, $"{what}: expected {expected}, got {r.Status}");
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrEmpty(r.Str("traceId")), what);
        Assert.Equal("id-test-1", r.Str("correlationId"));
    }
}
