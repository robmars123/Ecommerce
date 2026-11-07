using System.Text.Json;
using Ecommerce.Orders.Contracts;
using Ecommerce.Payments.Domain;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Payments.Application;

internal enum WebhookResult
{
    /// <summary>Handled, duplicate, unknown intent or an event type we ignore: all answer 200.</summary>
    Accepted,

    /// <summary>The signature is missing, bad or stale: 400, nothing read.</summary>
    BadSignature,
}

/// <summary>
/// The payment webhook (arch 8.2 rules 6, 8, 12; D3, D7, D8). Verifies outside any transaction, then in ONE transaction:
/// lock the payment by provider id, dedupe the event id, apply the payment change, the order move (through Orders
/// contract), the stock commit and the outbox rows. Any exception rolls everything back and surfaces as 500 so the
/// provider retries (Test 8). Only a duplicate or unknown intent answers 200 without a change.
/// </summary>
internal sealed class PaymentWebhookService(
    IWebhookVerifier verifier,
    IUnitOfWork unitOfWork,
    IPaymentStore store,
    IIdempotencyStore keys,
    IOrderPayments orders,
    IOutbox outbox,
    IAuditLog audit,
    IPaymentAlerts alerts,
    IClock clock)
{
    public const string EventScope = "stripe-event";
    private const string CancelledOrder = "Cancelled";
    private static readonly TimeSpan EventKeyLifetime = TimeSpan.FromDays(30);

    private sealed record AutoRefund(Guid PaymentId, Guid OrderId, Guid RefundId, string Reason);

    private sealed record Applied(AutoRefund? Refund, string? UnknownIntent);

    private sealed record PaymentAuditState(string Status, string? OrderStatus = null, Guid? RefundId = null, decimal? RefundAmount = null);

    public async Task<WebhookResult> HandleAsync(string body, string? signatureHeader, CancellationToken ct)
    {
        if (!verifier.TryVerify(body, signatureHeader, clock.UtcNow, out PaymentEvent? paymentEvent) || paymentEvent is null)
        {
            return WebhookResult.BadSignature;
        }
        if (paymentEvent.Kind == PaymentEventKind.Other)
        {
            return WebhookResult.Accepted;
        }

        Applied applied;
        try
        {
            applied = await unitOfWork.InTransactionAsync(token => ApplyAsync(paymentEvent, token), ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent delivery of the same event committed first (the event key or the automatic refund key): nothing more to do.
            return WebhookResult.Accepted;
        }

        // After commit, so a rolled-back attempt never raises an alert for money that was not recorded.
        if (applied.Refund is { } refund)
        {
            alerts.AutoRefundIssued(refund.PaymentId, refund.OrderId, refund.RefundId, refund.Reason);
        }
        if (applied.UnknownIntent is { } unknown)
        {
            alerts.UnknownIntent(paymentEvent.EventId, unknown);
        }
        return WebhookResult.Accepted;
    }

    private async Task<Applied> ApplyAsync(PaymentEvent evt, CancellationToken ct)
    {
        Payment? payment = await store.LockByProviderIdAsync(evt.ProviderPaymentId!, ct);
        if (payment is null)
        {
            return new Applied(null, evt.ProviderPaymentId);
        }
        if (await keys.FindAsync(EventScope, evt.EventId, ct) is not null)
        {
            return new Applied(null, null);
        }

        DateTimeOffset now = clock.UtcNow;
        keys.Add(new IdempotencyEntry(EventScope, evt.EventId, null, evt.EventType, payment.Id.ToString(), null, now, now + EventKeyLifetime));

        if (evt.Kind != PaymentEventKind.Succeeded)
        {
            payment.Fail(now);
            return new Applied(null, null);
        }

        string before = payment.Status.ToString();
        if (!payment.Succeed(now))
        {
            return new Applied(null, null);
        }

        MarkPaidOutcome outcome = await orders.TryMarkPaidAsync(payment.OrderId, ct);
        if (outcome.Paid)
        {
            await RequestCancelOfOtherAttemptsAsync(payment, now, ct);
            return new Applied(null, null);
        }

        string reason = outcome.OrderStatus == CancelledOrder ? Refund.LateSuccess : Refund.ExtraSuccess;
        Refund refund = Refund.CreateAutomatic(Guid.CreateVersion7(now), payment, reason, now);
        store.AddRefund(refund);
        outbox.Add(PaymentOutbox.RefundRequest(refund, payment, now));
        audit.Add(new AuditEntry(
            null, "payment.auto-refund", "payment", payment.Id,
            JsonSerializer.Serialize(new PaymentAuditState(before)),
            JsonSerializer.Serialize(new PaymentAuditState(payment.Status.ToString(), outcome.OrderStatus, refund.Id, refund.Amount)),
            reason, now));
        return new Applied(new AutoRefund(payment.Id, payment.OrderId, refund.Id, reason), null);
    }

    private async Task RequestCancelOfOtherAttemptsAsync(Payment paid, DateTimeOffset now, CancellationToken ct)
    {
        foreach (Payment other in await store.ListOpenIntentsAsync(paid.OrderId, paid.Id, ct))
        {
            outbox.Add(PaymentOutbox.CancelIntent(other, now));
        }
    }
}
