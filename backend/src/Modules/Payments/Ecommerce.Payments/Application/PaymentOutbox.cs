using System.Text.Json;
using Ecommerce.Payments.Domain;
using Ecommerce.SharedKernel.Outbox;

namespace Ecommerce.Payments.Application;

/// <summary>The outbox rows Payments writes (D9, D10). The consumers that act on them arrive with Slice 6; the payloads are internal until then.</summary>
internal static class PaymentOutbox
{
    public const string CancelRequested = "PaymentIntentCancelRequested";
    public const string RefundRequested = "RefundRequested";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal sealed record CancelIntentPayload(Guid PaymentId, Guid OrderId, string ProviderPaymentId, int Attempt);

    internal sealed record RefundRequestedPayload(
        Guid RefundId, Guid PaymentId, Guid OrderId, string? ProviderPaymentId, decimal Amount, string Currency, string Reason, string IdempotencyKey);

    public static OutboxMessage CancelIntent(Payment payment, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), CancelRequested, "Payment", payment.Id, 1,
            JsonSerializer.Serialize(new CancelIntentPayload(payment.Id, payment.OrderId, payment.ProviderPaymentId!, payment.Attempt), Json), now);

    public static OutboxMessage RefundRequest(Refund refund, Payment payment, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), RefundRequested, "Refund", refund.Id, 1,
            JsonSerializer.Serialize(
                new RefundRequestedPayload(refund.Id, payment.Id, payment.OrderId, payment.ProviderPaymentId, refund.Amount, refund.Currency, refund.Reason, refund.IdempotencyKey),
                Json),
            now);
}
