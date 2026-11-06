using System.Net;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// I-13: configured origins only, GET only, correlation id exposed, no credentials.
public class CatalogCorsTests
{
    private const string Allowed = "https://store.test.invalid";

    private static ApiFactory NewFactory() =>
        new(ApiFactory.Unreachable, settings: new Dictionary<string, string> { ["Cors:AllowedOrigins:0"] = Allowed });

    [Fact]
    public async Task Allowed_origin_is_echoed_and_the_correlation_header_is_exposed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = NewFactory();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", Allowed);

        HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(Allowed, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("X-Correlation-ID", string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Other_origin_gets_no_allow_origin_header()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = NewFactory();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "https://evil.test.invalid");

        HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_from_the_allowed_origin_succeeds_for_GET_and_advertises_only_GET()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = NewFactory();
        using HttpClient client = factory.CreateClient();

        using HttpRequestMessage get = new HttpRequestMessage(HttpMethod.Options, "/api/v1/products");
        get.Headers.Add("Origin", Allowed);
        get.Headers.Add("Access-Control-Request-Method", "GET");
        get.Headers.Add("Access-Control-Request-Headers", "x-correlation-id");
        HttpResponseMessage getResponse = await client.SendAsync(get, ct);

        Assert.Equal(HttpStatusCode.NoContent, getResponse.StatusCode);
        Assert.Equal(Allowed, Assert.Single(getResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("GET", string.Join(",", getResponse.Headers.GetValues("Access-Control-Allow-Methods")));

        using HttpRequestMessage post = new HttpRequestMessage(HttpMethod.Options, "/api/v1/products");
        post.Headers.Add("Origin", Allowed);
        post.Headers.Add("Access-Control-Request-Method", "POST");
        HttpResponseMessage postResponse = await client.SendAsync(post, ct);

        // Slice 1b contract change (plan D6, 1a D21): the policy now advertises exactly GET, POST and PUT.
        string[] advertised = string.Join(",", postResponse.Headers.GetValues("Access-Control-Allow-Methods"))
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Order()
            .ToArray();
        Assert.Equal(["GET", "POST", "PUT"], advertised);

        // ...and nothing wider: a DELETE preflight is refused (no allow-methods header lists DELETE).
        using HttpRequestMessage delete = new HttpRequestMessage(HttpMethod.Options, "/api/v1/products");
        delete.Headers.Add("Origin", Allowed);
        delete.Headers.Add("Access-Control-Request-Method", "DELETE");
        HttpResponseMessage deleteResponse = await client.SendAsync(delete, ct);
        string deleteAdvertised = deleteResponse.Headers.TryGetValues("Access-Control-Allow-Methods", out IEnumerable<string>? values) ? string.Join(",", values) : "";
        Assert.DoesNotContain("DELETE", deleteAdvertised);
    }

    [Fact]
    public async Task With_no_configured_origins_no_origin_is_allowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", Allowed);

        HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
