using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecommerce.Orders.Contracts;
using Ecommerce.Payments.Domain;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Payments.Application;

internal sealed record RefundCommand(Guid ActorId, Guid OrderId, decimal Amount, string Currency, string Reason, string IdempotencyKey);

/// <summary>The 202 body. Money is computed here, on the server (rule 1); a replay returns the stored copy.</summary>
internal sealed record RefundAccepted(Guid RefundId, decimal Amount, string Currency, string Status, decimal RefundedTotal, decimal Remaining);

/// <summary>Accepted | NotFound | Invalid | KeyConflict | Conflict (ceiling or order state).</summary>
internal abstract record RefundResult
{
    private RefundResult() { }

    public sealed record Accepted(RefundAccepted Value) : RefundResult;

    public sealed record NotFound : RefundResult;

    public sealed record Invalid(string Field, string Message) : RefundResult;

    public sealed record KeyConflict : RefundResult;

    public sealed record Conflict(string Type, string Message) : RefundResult;
}

/// <summary>
/// The admin refund (arch 8.5 steps 1 to 3). ONE transaction: lock the paying payment row (FOR UPDATE), check the ceiling,
/// lock the order, check the state rules, then insert the Pending refund, the audit row, the RefundRequested outbox row and
/// the idempotency key. The provider is called later by the outbox consumer, never here (rule 6). Lock order: payments, then orders.
/// </summary>
internal sealed class RefundService(
    IUnitOfWork unitOfWork,
    IPaymentStore store,
    IIdempotencyStore keys,
    IOrderRefunds orders,
    IOutbox outbox,
    IAuditLog audit,
    IClock clock)
{
    public const string Scope = "refund";
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record RefundAuditState(string PaymentStatus, decimal RefundedTotal, decimal PendingTotal, Guid? RefundId = null, decimal? Amount = null);

    private sealed record RejectedAuditState(decimal Requested, decimal Committed, decimal Captured);

    public async Task<RefundResult> RequestAsync(RefundCommand command, CancellationToken ct)
    {
        string hash = HashOf(command);
        if (Replay(await keys.FindAsync(Scope, command.IdempotencyKey, ct), hash) is { } seen)
        {
            return seen;
        }

        try
        {
            return await unitOfWork.InTransactionAsync(c => ExecuteAsync(command, hash, c), ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // A concurrent request with the same key committed first: answer from what it stored.
            return Replay(await keys.FindAsync(Scope, command.IdempotencyKey, ct), hash)
                   ?? new RefundResult.Conflict("refund-conflict", "The refund could not be recorded; try again.");
        }
    }

    private async Task<RefundResult> ExecuteAsync(RefundCommand command, string hash, CancellationToken ct)
    {
        Payment? payment = await store.LockPayingPaymentAsync(command.OrderId, ct);
        if (payment is null)
        {
            // Nothing captured. Tell a missing order from one that was never paid; the order lock is the first lock held.
            return await orders.LockForRefundAsync(command.OrderId, ct) is null
                ? new RefundResult.NotFound()
                : new RefundResult.Conflict("no-refundable-payment", "This order has no captured payment to refund.");
        }
        if (Replay(await keys.FindAsync(Scope, command.IdempotencyKey, ct), hash) is { } seen)
        {
            return seen;
        }
        if (!string.Equals(command.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return new RefundResult.Invalid("amount.currency", "The refund currency must be the order's currency.");
        }
        if (command.Amount != decimal.Round(command.Amount, 2))
        {
            return new RefundResult.Invalid("amount.amount", "Enter an amount with at most 2 decimals.");
        }

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyCollection<Guid> none = [];
        decimal succeeded = await store.SumRefundsAsync(payment.Id, [RefundStatus.Succeeded], none, ct);
        decimal pending = await store.SumRefundsAsync(payment.Id, [RefundStatus.Pending], none, ct);
        string before = JsonSerializer.Serialize(new RefundAuditState(payment.Status.ToString(), succeeded, pending), Json);

        if (!payment.CanCover(succeeded + pending, command.Amount))
        {
            // Audited in this same short transaction; no refund row and no idempotency key, so a later retry can still succeed.
            audit.Add(new AuditEntry(
                command.ActorId, "refund.rejected", "payment", payment.Id, before,
                JsonSerializer.Serialize(new RejectedAuditState(command.Amount, succeeded + pending, payment.Amount), Json), command.Reason, now));
            return new RefundResult.Conflict("refund-ceiling-exceeded", "The refunds would exceed the amount captured.");
        }

        RefundOrderInfo? order = await orders.LockForRefundAsync(command.OrderId, ct);
        if (order is null)
        {
            return new RefundResult.NotFound();
        }
        bool full = succeeded + pending + command.Amount == payment.Amount;
        if (!StateAllows(order, full))
        {
            return new RefundResult.Conflict(
                "refund-state-not-allowed",
                full ? $"A full refund is not possible while the order is {order.Status}." : $"A refund is not possible while the order is {order.Status}.");
        }

        Refund refund = Refund.CreateAdmin(Guid.CreateVersion7(now), payment, command.Amount, command.Reason, command.ActorId, command.IdempotencyKey, now);
        store.AddRefund(refund);
        outbox.Add(PaymentOutbox.RefundRequest(refund, payment, now));
        decimal pendingAfter = pending + command.Amount;
        audit.Add(new AuditEntry(
            command.ActorId, "refund.request", "refund", refund.Id, before,
            JsonSerializer.Serialize(new RefundAuditState(payment.Status.ToString(), succeeded, pendingAfter, refund.Id, refund.Amount), Json),
            command.Reason, now));

        RefundAccepted accepted = new(refund.Id, refund.Amount, refund.Currency, RefundStatus.Pending.ToString(), succeeded, payment.Amount - succeeded - pendingAfter);
        keys.Add(new IdempotencyEntry(
            Scope, command.IdempotencyKey, command.ActorId, hash, refund.Id.ToString(), JsonSerializer.Serialize(accepted, Json), now, now + KeyLifetime));
        return new RefundResult.Accepted(accepted);
    }

    // arch 8.5 step 2: a partial refund from Paid..Delivered; a refund that reaches the captured amount only where 7.3 has an edge.
    private static bool StateAllows(RefundOrderInfo order, bool full) =>
        full
            ? order.Status is "Paid" or "Processing" || (order.Status == "Delivered" && order.HasApprovedReturn)
            : order.Status is "Paid" or "Processing" or "Shipped" or "Delivered";

    private static RefundResult? Replay(IdempotencyEntry? seen, string hash)
    {
        if (seen is null)
        {
            return null;
        }
        if (seen.RequestHash != hash || seen.ResponseJson is null)
        {
            return new RefundResult.KeyConflict();
        }
        return new RefundResult.Accepted(JsonSerializer.Deserialize<RefundAccepted>(seen.ResponseJson, Json)!);
    }

    private static string HashOf(RefundCommand c)
    {
        string text = string.Create(
            CultureInfo.InvariantCulture, $"{c.OrderId}|{c.Amount:0.0000}|{c.Currency.ToUpperInvariant()}|{c.Reason}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
