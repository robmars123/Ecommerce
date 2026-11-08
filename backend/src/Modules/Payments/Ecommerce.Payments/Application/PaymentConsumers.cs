using System.Text.Json;
using Ecommerce.SharedKernel.Outbox;

namespace Ecommerce.Payments.Application;

/// <summary>
/// RefundRequested to the provider (arch 8.5 step 4, D10). the provider idempotency key is <c>re:{refundId}</c>, so a retry after a
/// crash between the call and the mark returns the same provider refund. The refund stays Pending until the webhook confirms.
/// </summary>
internal sealed class RefundRequestedConsumer(IPaymentGateway gateway, IPaymentStore store) : IOutboxConsumer
{
    public string Name => "payments.refund-requested";

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string> { PaymentOutbox.RefundRequested };

    public static string ProviderKey(Guid refundId) => $"re:{refundId}";

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        PaymentOutbox.RefundRequestedPayload payload = JsonSerializer.Deserialize<PaymentOutbox.RefundRequestedPayload>(message.PayloadJson, PaymentOutbox.Json)!;
        if (payload.ProviderPaymentId is null)
        {
            throw new InvalidOperationException($"Refund {payload.RefundId} has no provider payment to refund yet.");
        }

        string providerRefundId = await gateway.CreateRefundAsync(
            new RefundCall(payload.ProviderPaymentId, MinorUnits.From(payload.Amount), payload.Currency, payload.RefundId, ProviderKey(payload.RefundId)), ct);
        await store.SetProviderRefundIdAsync(payload.RefundId, providerRefundId, ct);
    }
}

/// <summary>PaymentIntentCancelRequested to the provider (closes Slice 4 D9). An intent that is already canceled or paid is not an error.</summary>
internal sealed class IntentCancelConsumer(IPaymentGateway gateway) : IOutboxConsumer
{
    public string Name => "payments.intent-cancel";

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string> { PaymentOutbox.CancelRequested };

    public Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        PaymentOutbox.CancelIntentPayload payload = JsonSerializer.Deserialize<PaymentOutbox.CancelIntentPayload>(message.PayloadJson, PaymentOutbox.Json)!;
        return gateway.CancelIntentAsync(payload.ProviderPaymentId, ct);
    }
}
