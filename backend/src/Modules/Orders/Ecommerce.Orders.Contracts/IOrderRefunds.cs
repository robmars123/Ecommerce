namespace Ecommerce.Orders.Contracts;

/// <summary>The order as the refund path needs it: its status, its owner, and whether an Approved return request is open.</summary>
public sealed record RefundOrderInfo(string Status, Guid UserId, bool HasApprovedReturn);

/// <summary>
/// Orders' side of refunds (arch 8.5, Slice 6 D7). Both methods run inside the caller's transaction, after the caller holds the
/// payment row lock (lock order payments, orders). Neither calls a provider.
/// </summary>
public interface IOrderRefunds
{
    /// <summary>Locks the order FOR UPDATE. Null when the order does not exist.</summary>
    Task<RefundOrderInfo?> LockForRefundAsync(Guid orderId, CancellationToken ct);

    /// <summary>
    /// A refund reached the captured amount: Paid or Processing becomes Refunded, Delivered becomes Returned (its Approved return
    /// request becomes Refunded). History row and one OrderStatusChanged event. False, changing nothing, when no transition applies.
    /// </summary>
    Task<bool> ApplyFullRefundAsync(Guid orderId, Guid refundId, Guid? actorId, CancellationToken ct);

    /// <summary>A partial refund succeeded: an Approved return request on the order becomes Refunded. The order status is untouched.</summary>
    Task MarkReturnRefundedAsync(Guid orderId, CancellationToken ct);
}
