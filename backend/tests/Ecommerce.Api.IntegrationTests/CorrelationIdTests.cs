using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests;

public partial class CorrelationIdTests
{
    private const string Header = "X-Correlation-ID";

    [GeneratedRegex("^[a-f0-9]{32}$")]
    private static partial Regex GeneratedId();

    [Fact]
    public async Task Generates_and_echoes_id_when_header_absent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live", ct);

        Assert.True(response.Headers.TryGetValues(Header, out IEnumerable<string>? values));
        Assert.Matches(GeneratedId(), Assert.Single(values));
    }

    [Fact]
    public async Task Echoes_valid_inbound_id()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(Header, "abc-123._:XYZ");

        HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal("abc-123._:XYZ", Assert.Single(response.Headers.GetValues(Header)));
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task Replaces_invalid_inbound_id(string inbound)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(Header, inbound);

        HttpResponseMessage response = await client.SendAsync(request, ct);

        string echoed = Assert.Single(response.Headers.GetValues(Header));
        Assert.NotEqual(inbound, echoed);
        Assert.Matches(GeneratedId(), echoed);
    }

    [Fact]
    public async Task Request_log_event_carries_the_correlation_id()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CollectingSink sink = new CollectingSink();
        await using ApiFactory factory = new ApiFactory(
            ApiFactory.Unreachable,
            s => s.AddSingleton<ILogEventSink>(sink),
            new Dictionary<string, string> { ["Serilog:MinimumLevel:Default"] = "Debug" });
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(Header, "log-check-1");

        await client.SendAsync(request, ct);

        Assert.Contains(sink.Events, e =>
            IsRequestCompletion(e) &&
            e.Properties.TryGetValue("CorrelationId", out LogEventPropertyValue? id) && id.ToString() == "\"log-check-1\"" &&
            e.Properties.TryGetValue("RequestPath", out LogEventPropertyValue? path) && path.ToString() == "\"/health/live\"");
    }

    [Fact]
    public async Task Unknown_route_request_log_is_information_with_correlation_id()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CollectingSink sink = new CollectingSink();
        await using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable, s => s.AddSingleton<ILogEventSink>(sink));
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/no-such-route");
        request.Headers.Add(Header, "log-check-404");

        HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        LogEvent completion = Assert.Single(sink.Events, IsRequestCompletion);
        Assert.Equal(Serilog.Events.LogEventLevel.Information, completion.Level);
        Assert.Equal("\"log-check-404\"", completion.Properties["CorrelationId"].ToString());
        Assert.Equal("404", completion.Properties["StatusCode"].ToString());
    }

    private static bool IsRequestCompletion(Serilog.Events.LogEvent e) =>
        e.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? ctx) &&
        ctx.ToString() == "\"Serilog.AspNetCore.RequestLoggingMiddleware\"" &&
        e.Properties.ContainsKey("StatusCode");
}
