namespace Ecommerce.Orders.Contracts;

/// <summary>Whether the order was moved to Paid, and its status afterwards (or the status that blocked the move).</summary>
public sealed record MarkPaidOutcome(bool Paid, string OrderStatus);

/// <summary>
/// Orders' side of a verified payment (arch 8.2 rules 6, 8, 12). Runs inside the caller's transaction: locks the order
/// FOR UPDATE, and only a PendingPayment order becomes Paid (history, stock commit, cart lines removed, OrderPaid event).
/// Any other status changes nothing and is reported, so the caller can refund.
/// </summary>
public interface IOrderPayments
{
    Task<MarkPaidOutcome> TryMarkPaidAsync(Guid orderId, CancellationToken ct);
}
