using System.Net;
using System.Text.Json;

namespace Ecommerce.Api.IntegrationTests;

public class ProblemDetailsTests
{
    [Fact]
    public async Task Unknown_route_returns_problem_details_with_trace_and_correlation_ids()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(ApiFactory.Unreachable);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/does-not-exist");
        request.Headers.Add("X-Correlation-ID", "pd-check-1");

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        Assert.Equal("pd-check-1", root.GetProperty("correlationId").GetString());
        Assert.False(root.TryGetProperty("exception", out _));
        Assert.False(root.TryGetProperty("stack", out _));
    }
}
