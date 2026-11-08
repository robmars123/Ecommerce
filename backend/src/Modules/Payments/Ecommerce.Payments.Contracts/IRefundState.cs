namespace Ecommerce.Payments.Contracts;

public sealed record RefundInfo(Guid RefundId, decimal Amount, string Status, DateTimeOffset CreatedAt);

/// <summary>
/// Everything computed on the server (rule 1). <see cref="Captured"/> is 0 and the lists are empty when the order has no paying
/// payment. <see cref="Remaining"/> is captured minus succeeded minus pending: what an admin may still refund.
/// </summary>
public sealed record RefundSummary(
    string? Currency, decimal Captured, decimal RefundedTotal, decimal PendingTotal, decimal Remaining, IReadOnlyList<RefundInfo> Refunds);

/// <summary>Payments' read side for Orders. No locks: Orders calls it after taking its own order lock (lock order, Slice 6 D8).</summary>
public interface IRefundState
{
    /// <summary>True while a Pending refund exists and Pending plus Succeeded refunds already cover the captured amount.</summary>
    Task<bool> HasPendingFullRefundAsync(Guid orderId, CancellationToken ct);

    Task<RefundSummary> GetSummaryAsync(Guid orderId, CancellationToken ct);
}
