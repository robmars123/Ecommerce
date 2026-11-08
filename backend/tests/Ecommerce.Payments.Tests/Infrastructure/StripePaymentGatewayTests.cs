using System.Net;
using System.Text;
using Ecommerce.Payments.Application;
using Ecommerce.Payments.Contracts;
using Ecommerce.Payments.Infrastructure;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payments.Tests.Infrastructure;

// Refund and cancel against a stubbed HTTP handler: no network, but the exact request the SDK sends is checked.
public class StripePaymentGatewayTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static StripePaymentGateway Gateway(StubHandler handler, string key = "sk_placeholder_for_tests") =>
        new(Options.Create(new StripeOptions { SecretKey = key }), new HttpClient(handler));

    [Fact]
    public async Task A_refund_posts_the_intent_the_minor_unit_amount_the_refund_id_metadata_and_the_idempotency_key()
    {
        StubHandler handler = new(HttpStatusCode.OK, "{\"id\":\"re_123\",\"object\":\"refund\",\"status\":\"pending\"}");
        Guid refundId = Guid.NewGuid();

        string providerId = await Gateway(handler).CreateRefundAsync(new RefundCall("pi_77", 1234, "USD", refundId, $"re:{refundId}"), Ct);

        Assert.Equal("re_123", providerId);
        (HttpRequestMessage request, string body) = Assert.Single(handler.Seen);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/refunds", request.RequestUri!.AbsolutePath);
        Assert.Equal($"re:{refundId}", Assert.Single(request.Headers.GetValues("Idempotency-Key")));
        string decoded = Uri.UnescapeDataString(body);
        Assert.Contains("payment_intent=pi_77", decoded);
        Assert.Contains("amount=1234", decoded);
        Assert.Contains($"metadata[refund_id]={refundId}", decoded);
    }

    [Fact]
    public async Task A_cancel_posts_to_the_intent_cancel_endpoint()
    {
        StubHandler handler = new(HttpStatusCode.OK, "{\"id\":\"pi_5\",\"object\":\"payment_intent\",\"status\":\"canceled\"}");

        await Gateway(handler).CancelIntentAsync("pi_5", Ct);

        (HttpRequestMessage request, _) = Assert.Single(handler.Seen);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/payment_intents/pi_5/cancel", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task A_cancel_of_an_intent_in_an_unexpected_state_counts_as_done()
    {
        StubHandler handler = new(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"type\":\"invalid_request_error\",\"code\":\"payment_intent_unexpected_state\",\"message\":\"already canceled\"}}");

        await Gateway(handler).CancelIntentAsync("pi_5", Ct);

        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task Any_other_provider_error_on_cancel_or_refund_is_ProviderUnavailable_so_the_outbox_retries()
    {
        StubHandler handler = new(HttpStatusCode.BadRequest, "{\"error\":{\"type\":\"invalid_request_error\",\"code\":\"resource_missing\",\"message\":\"no such intent\"}}");
        StripePaymentGateway gateway = Gateway(handler);

        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() => gateway.CancelIntentAsync("pi_5", Ct));
        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(
            () => gateway.CreateRefundAsync(new RefundCall("pi_5", 100, "USD", Guid.NewGuid(), "re:x"), Ct));
    }

    [Fact]
    public async Task A_missing_secret_key_never_calls_the_provider()
    {
        StubHandler handler = new(HttpStatusCode.OK, "{}");

        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(
            () => Gateway(handler, key: "").CreateRefundAsync(new RefundCall("pi_5", 100, "USD", Guid.NewGuid(), "re:x"), Ct));

        Assert.Empty(handler.Seen);
    }
}
