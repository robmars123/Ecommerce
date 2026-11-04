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
        var ct = TestContext.Current.CancellationToken;
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", Allowed);

        var response = await client.SendAsync(request, ct);

        Assert.Equal(Allowed, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("X-Correlation-ID", string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Other_origin_gets_no_allow_origin_header()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "https://evil.test.invalid");

        var response = await client.SendAsync(request, ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_from_the_allowed_origin_succeeds_for_GET_and_advertises_only_GET()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = NewFactory();
        using var client = factory.CreateClient();

        using var get = new HttpRequestMessage(HttpMethod.Options, "/api/v1/products");
        get.Headers.Add("Origin", Allowed);
        get.Headers.Add("Access-Control-Request-Method", "GET");
        get.Headers.Add("Access-Control-Request-Headers", "x-correlation-id");
        var getResponse = await client.SendAsync(get, ct);

        Assert.Equal(HttpStatusCode.NoContent, getResponse.StatusCode);
        Assert.Equal(Allowed, Assert.Single(getResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("GET", string.Join(",", getResponse.Headers.GetValues("Access-Control-Allow-Methods")));

        using var post = new HttpRequestMessage(HttpMethod.Options, "/api/v1/products");
        post.Headers.Add("Origin", Allowed);
        post.Headers.Add("Access-Control-Request-Method", "POST");
        var postResponse = await client.SendAsync(post, ct);

        // The policy advertises GET only, so a browser will refuse the POST.
        Assert.Equal("GET", string.Join(",", postResponse.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Fact]
    public async Task With_no_configured_origins_no_origin_is_allowed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(ApiFactory.Unreachable);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", Allowed);

        var response = await client.SendAsync(request, ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
