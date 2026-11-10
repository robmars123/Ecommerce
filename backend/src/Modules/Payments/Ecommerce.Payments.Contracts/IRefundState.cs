namespace Ecommerce.Payments.Contracts;

public sealed record RefundInfo(Guid RefundId, decimal Amount, string Status, DateTimeOffset CreatedAt);

/// <summary>
/// Everything computed on the server (rule 1). <see cref="Captured"/> is 0 and the lists are empty when the order has no paying
/// payment. <see cref="Remaining"/> is captured minus succeeded minus pending: what an admin may still refund.
/// </summary>
public sealed record RefundSummary(
    string? Currency, decimal Captured, decimal RefundedTotal, decimal PendingTotal, decimal Remaining, IReadOnlyList<RefundInfo> Refunds);

public sealed record OrderRefundInfo(Guid RefundId, Guid OrderId, decimal Amount, string Currency, string Status, bool Automatic, DateTimeOffset CreatedAt);

/// <summary>
/// Refunds across a set of orders. <see cref="ActorSucceededTotal"/> sums Succeeded refunds made by an admin (an actor); automatic
/// refunds (late or duplicate captures) are excluded because that money was never part of an order's paid total.
/// </summary>
public sealed record CustomerRefunds(decimal ActorSucceededTotal, int Count, IReadOnlyList<OrderRefundInfo> Recent);

/// <summary>Payments' read side for Orders. No locks: Orders calls it after taking its own order lock (lock order, Slice 6 D8).</summary>
public interface IRefundState
{
    /// <summary>True while a Pending refund exists and Pending plus Succeeded refunds already cover the captured amount.</summary>
    Task<bool> HasPendingFullRefundAsync(Guid orderId, CancellationToken ct);

    Task<RefundSummary> GetSummaryAsync(Guid orderId, CancellationToken ct);

    /// <summary>Refunds of the orders' payments, newest first; <see cref="CustomerRefunds.Recent"/> holds at most <paramref name="recentTake"/>. No locks.</summary>
    Task<CustomerRefunds> GetForOrdersAsync(IReadOnlyCollection<Guid> orderIds, int recentTake, CancellationToken ct);
}
