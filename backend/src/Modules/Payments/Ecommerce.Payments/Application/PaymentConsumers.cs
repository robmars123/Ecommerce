using System.Text.Json;
using Ecommerce.Orders.Contracts;
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

/// <summary>
/// FulfillmentFailed to a refund (arch 8.8.4, Slice 6b D18). It asks the one refund path for the amount the saga fixed, keyed
/// <c>saga:{sagaId}:Fulfill</c>: a repeat (a retry, or a run whose mark was lost) has the same key and the same request, so it
/// gets the stored answer. Anything but an accepted refund throws, so the runtime retries and finally dead-letters (it is alerted).
/// </summary>
internal sealed class FulfillmentCompensationConsumer(RefundService refunds) : IOutboxConsumer
{
    public const string ConsumerName = "payments.fulfillment-compensation";

    public string Name => ConsumerName;

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string> { FulfillmentEventTypes.FulfillmentFailed };

    public static string SagaKey(Guid sagaId) => $"saga:{sagaId}:Fulfill";

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        FulfillmentFailedPayload payload = JsonSerializer.Deserialize<FulfillmentFailedPayload>(message.PayloadJson, PaymentOutbox.Json)!;
        if (payload.RefundAmount <= 0m)
        {
            // Nothing is left to refund; a pending full refund (if any) will finish the saga when it lands.
            return;
        }

        RefundResult result = await refunds.RequestAsync(
            new RefundCommand(payload.ActorId, payload.OrderId, payload.RefundAmount, payload.Currency, payload.Reason, SagaKey(payload.SagaId)), ct);
        if (result is not RefundResult.Accepted)
        {
            throw new InvalidOperationException($"The compensation refund for saga {payload.SagaId} was not accepted ({result.GetType().Name}).");
        }
    }
}
