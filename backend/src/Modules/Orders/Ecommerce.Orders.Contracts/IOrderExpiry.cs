namespace Ecommerce.Orders.Contracts;

/// <summary>
/// Orders' side of reservation expiry (arch 8.2 rule 7). Runs inside the caller's transaction: locks the order FOR UPDATE;
/// a PendingPayment order becomes Cancelled (reason reservation-expired) and its open payment intent is cancelled through
/// the outbox. Returns false, changing nothing, for an unknown order or any other status.
/// </summary>
public interface IOrderExpiry
{
    Task<bool> CancelExpiredAsync(Guid orderId, CancellationToken ct);
}
