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
    IOrderRefunds refunds,
    IOutbox outbox,
    IAuditLog audit,
    IPaymentAlerts alerts,
    IClock clock)
{
    public const string EventScope = "stripe-event";
    private const string CancelledOrder = "Cancelled";
    private static readonly TimeSpan EventKeyLifetime = TimeSpan.FromDays(30);

    private sealed record AutoRefund(Guid PaymentId, Guid OrderId, Guid RefundId, string Reason);

    private sealed record RefundProblem(Guid RefundId, Guid PaymentId, Guid? OrderId);

    private sealed record Applied(
        AutoRefund? Refund, string? UnknownIntent, IReadOnlyList<RefundProblem>? FailedRefunds = null, IReadOnlyList<RefundProblem>? BlockedOrders = null);

    private sealed record RefundAuditState(string Status, string PaymentStatus, bool? OrderTransitioned = null);

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
        foreach (RefundProblem failed in applied.FailedRefunds ?? [])
        {
            alerts.RefundFailed(failed.RefundId, failed.PaymentId);
        }
        foreach (RefundProblem blocked in applied.BlockedOrders ?? [])
        {
            alerts.RefundOrderNotTransitioned(blocked.RefundId, blocked.OrderId!.Value);
        }
        if (applied.UnknownIntent is { } unknown)
        {
            alerts.UnknownIntent(paymentEvent.EventId, unknown);
        }
        return WebhookResult.Accepted;
    }

    private async Task<Applied> ApplyAsync(PaymentEvent evt, CancellationToken ct)
    {
        if (evt.Kind == PaymentEventKind.Refund)
        {
            return await ApplyRefundAsync(evt, ct);
        }

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

        // The money that actually moved must equal the payment row; otherwise it is not a valid payment (arch 8.2 rules 1, 5).
        bool mismatch = evt.AmountReceived != MinorUnits.From(payment.Amount)
                        || !string.Equals(evt.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase);
        MarkPaidOutcome? outcome = mismatch ? null : await orders.TryMarkPaidAsync(payment.OrderId, ct);
        if (outcome is { Paid: true })
        {
            await RequestCancelOfOtherAttemptsAsync(payment, now, ct);
            return new Applied(null, null);
        }

        string reason = mismatch ? Refund.AmountMismatch : outcome!.OrderStatus == CancelledOrder ? Refund.LateSuccess : Refund.ExtraSuccess;
        Refund refund = mismatch
            ? Refund.CreateAutomatic(Guid.CreateVersion7(now), payment, reason, now, evt.AmountReceived!.Value / 100m, evt.Currency!.ToUpperInvariant())
            : Refund.CreateAutomatic(Guid.CreateVersion7(now), payment, reason, now);
        store.AddRefund(refund);
        outbox.Add(PaymentOutbox.RefundRequest(refund, payment, now));
        audit.Add(new AuditEntry(
            null, "payment.auto-refund", "payment", payment.Id,
            JsonSerializer.Serialize(new PaymentAuditState(before)),
            JsonSerializer.Serialize(new PaymentAuditState(payment.Status.ToString(), outcome?.OrderStatus, refund.Id, refund.Amount)),
            reason, now));
        return new Applied(new AutoRefund(payment.Id, payment.OrderId, refund.Id, reason), null);
    }

    /// <summary>
    /// charge.refunded / refund.updated (arch 8.5 step 5, D11). Lock order payments then orders. The refund is found by our id
    /// (metadata.refund_id), else by the provider refund id; the payment row is locked before anything is read for update.
    /// A second terminal event for a refund is a no-op. Order totals are never touched.
    /// </summary>
    private async Task<Applied> ApplyRefundAsync(PaymentEvent evt, CancellationToken ct)
    {
        List<(RefundNotice Notice, Guid RefundId, Guid PaymentId)> known = [];
        foreach (RefundNotice notice in evt.Refunds ?? [])
        {
            Refund? found = await store.FindRefundAsync(notice.RefundId, notice.ProviderRefundId, ct);
            if (found is not null)
            {
                known.Add((notice, found.Id, found.PaymentId));
            }
        }
        if (known.Count == 0)
        {
            return new Applied(null, evt.Refunds?.FirstOrDefault()?.ProviderRefundId ?? evt.EventType);
        }

        // One event reports the refunds of one charge, so one payment.
        Guid paymentId = known[0].PaymentId;
        Payment payment = await store.LockByIdAsync(paymentId, ct) ?? throw new InvalidOperationException($"Payment {paymentId} vanished.");
        if (await keys.FindAsync(EventScope, evt.EventId, ct) is not null)
        {
            return new Applied(null, null);
        }

        DateTimeOffset now = clock.UtcNow;
        keys.Add(new IdempotencyEntry(EventScope, evt.EventId, null, evt.EventType, payment.Id.ToString(), null, now, now + EventKeyLifetime));

        List<Refund> changed = [];
        foreach ((RefundNotice notice, Guid refundId, Guid _) in known.Where(k => k.PaymentId == paymentId))
        {
            Refund refund = await store.GetRefundTrackedAsync(refundId, ct) ?? throw new InvalidOperationException($"Refund {refundId} vanished.");
            bool moved = notice.Status switch
            {
                RefundNoticeStatus.Succeeded => refund.Succeed(notice.ProviderRefundId, now),
                RefundNoticeStatus.Failed => refund.Fail(now),
                _ => false,
            };
            if (moved)
            {
                changed.Add(refund);
            }
        }
        if (changed.Count == 0)
        {
            return new Applied(null, null);
        }

        IReadOnlyCollection<Guid> touched = changed.Select(r => r.Id).ToList();
        decimal succeededTotal = await store.SumRefundsAsync(payment.Id, [RefundStatus.Succeeded], touched, ct)
                                 + changed.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount);
        string paymentBefore = payment.Status.ToString();
        payment.ApplyRefundedTotal(succeededTotal, now, changed.Any(r => r.Status == RefundStatus.Succeeded && r.ActorId is null));

        List<RefundProblem> failed = changed
            .Where(r => r.Status == RefundStatus.Failed)
            .Select(r => new RefundProblem(r.Id, payment.Id, payment.OrderId))
            .ToList();
        List<RefundProblem> blocked = [];

        // The order effect is decided once per event. An automatic refund returns money the order could not use; it never
        // moves the order (rules 8 and 12).
        Refund? adminSucceeded = changed.FirstOrDefault(r => r.Status == RefundStatus.Succeeded && r.ActorId is not null);
        bool? transitioned = null;
        if (adminSucceeded is not null)
        {
            if (succeededTotal >= payment.Amount)
            {
                transitioned = await refunds.ApplyFullRefundAsync(payment.OrderId, adminSucceeded.Id, adminSucceeded.ActorId, ct);
                if (transitioned == false)
                {
                    blocked.Add(new RefundProblem(adminSucceeded.Id, payment.Id, payment.OrderId));
                }
            }
            else
            {
                await refunds.MarkReturnRefundedAsync(payment.OrderId, ct);
            }
        }

        foreach (Refund refund in changed)
        {
            bool? outcome = refund.Status == RefundStatus.Succeeded && refund.ActorId is not null ? transitioned : null;
            audit.Add(new AuditEntry(
                null, refund.Status == RefundStatus.Succeeded ? "refund.succeeded" : "refund.failed", "refund", refund.Id,
                JsonSerializer.Serialize(new RefundAuditState(RefundStatus.Pending.ToString(), paymentBefore)),
                JsonSerializer.Serialize(new RefundAuditState(refund.Status.ToString(), payment.Status.ToString(), outcome)),
                null, now));
        }
        return new Applied(null, null, failed, blocked);
    }

    private async Task RequestCancelOfOtherAttemptsAsync(Payment paid, DateTimeOffset now, CancellationToken ct)
    {
        foreach (Payment other in await store.ListOpenIntentsAsync(paid.OrderId, paid.Id, ct))
        {
            outbox.Add(PaymentOutbox.CancelIntent(other, now));
        }
    }
}
