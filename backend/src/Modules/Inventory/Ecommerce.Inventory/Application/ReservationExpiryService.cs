using Ecommerce.Inventory.Domain;
using Ecommerce.Orders.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Inventory.Application;

/// <summary>
/// Expires reservations whose time ran out and gives the stock back (arch 8.2 rule 7, D5, Slice 4 D11). One transaction per
/// order: Orders cancels the order first (it locks the order row), then lock that order's Active reservations, re-check the
/// deadline under the lock, lock the stock rows (variant-ID order), expire, release. Lock order is orders, reservations,
/// stock (D3). Running it twice, or on two instances, releases once. One failing order does not stop the others.
/// </summary>
internal sealed class ReservationExpiryService(IInventoryStore store, IUnitOfWork unitOfWork, IClock clock, IOrderExpiry orderExpiry)
{
    public const int BatchSize = 100;

    /// <summary>
    /// Returns how many reservations were expired. If any order failed, the others are still done and the failures are then
    /// thrown together as an <see cref="AggregateException"/> (the worker logs it).
    /// </summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<Guid> orderIds = await store.FindExpiredOrderIdsAsync(now, BatchSize, ct);
        int expired = 0;
        List<Exception> failures = [];
        foreach (Guid orderId in orderIds)
        {
            try
            {
                expired += await unitOfWork.InTransactionAsync(token => ExpireOrderAsync(orderId, now, token), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new InvalidOperationException($"Expiring order {orderId} failed.", ex));
            }
        }
        if (failures.Count > 0)
        {
            throw new AggregateException($"{failures.Count} of {orderIds.Count} orders failed to expire; the rest were processed.", failures);
        }
        return expired;
    }

    private async Task<int> ExpireOrderAsync(Guid orderId, DateTimeOffset now, CancellationToken ct)
    {
        bool cancelled = await orderExpiry.CancelExpiredAsync(orderId, ct);
        IReadOnlyList<InventoryReservation> active = await store.LockActiveReservationsAsync(orderId, ct);
        List<InventoryReservation> due = active.Where(r => r.ExpiresAt <= now).ToList();
        if (due.Count == 0)
        {
            if (cancelled)
            {
                // Never leave an order Cancelled with its stock still held: undo the cancel (the transaction rolls back).
                throw new InvalidOperationException($"Order {orderId} was cancelled but none of its reservations is due.");
            }
            return 0;
        }

        IReadOnlyList<InventoryStock> stock = await store.LockAsync(due.Select(r => r.ProductVariantId).Distinct().ToList(), ct);
        Dictionary<Guid, InventoryStock> rows = stock.ToDictionary(s => s.ProductVariantId);
        foreach (InventoryReservation reservation in due)
        {
            reservation.Expire();
            rows[reservation.ProductVariantId].Release(reservation.Quantity);
        }
        return due.Count;
    }
}
