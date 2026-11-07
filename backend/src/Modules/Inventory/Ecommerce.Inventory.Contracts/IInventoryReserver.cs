namespace Ecommerce.Inventory.Contracts;

public sealed record ReservationLine(Guid VariantId, int Quantity);

public sealed record ReservationShortfall(Guid VariantId, int Requested, int Available);

public sealed record ReservationResult(bool Succeeded, IReadOnlyList<ReservationShortfall> Shortfalls);

/// <summary>
/// Reserves stock for an order (arch 8.2). Must run inside the caller's <c>IUnitOfWork.InTransactionAsync</c> so the
/// reservation and the order commit together. Locks stock rows in variant-ID order (rule 3). A shortfall is a normal
/// <see cref="ReservationResult"/>, never an exception; bad input (no lines, quantity 0 or less, duplicate variant) is
/// an <see cref="ArgumentException"/>.
/// </summary>
public interface IInventoryReserver
{
    Task<ReservationResult> ReserveAsync(
        Guid orderId, IReadOnlyList<ReservationLine> lines, DateTimeOffset expiresAt, CancellationToken ct);

    /// <summary>
    /// Commits the order's Active reservations on Paid (rule 3): reservations first, then stock rows in variant-ID order;
    /// on_hand and reserved each fall by the reserved quantity. Inside the caller's transaction. An order with no Active
    /// reservation is an <see cref="InvalidOperationException"/> (money arrived for stock that was already released).
    /// </summary>
    Task CommitAsync(Guid orderId, CancellationToken ct);

    /// <summary>The earliest <c>expires_at</c> among the order's Active reservations, or null if it has none. Inside the caller's transaction.</summary>
    Task<DateTimeOffset?> GetActiveExpiryAsync(Guid orderId, CancellationToken ct);
}
