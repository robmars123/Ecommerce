using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Domain;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Inventory.Application;

/// <summary>
/// Reserves stock inside the caller's transaction (arch 8.2, D1 to D3). Locks every involved stock row in variant-ID
/// order in one query, checks every line, then changes nothing unless all lines fit. The caller's transaction commits it.
/// </summary>
internal sealed class InventoryReserver(IInventoryStore store, IClock clock) : IInventoryReserver
{
    public async Task<ReservationResult> ReserveAsync(
        Guid orderId, IReadOnlyList<ReservationLine> lines, DateTimeOffset expiresAt, CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(lines));
        }
        if (lines.Any(l => l.Quantity <= 0))
        {
            throw new ArgumentException("Every quantity must be greater than zero.", nameof(lines));
        }
        if (lines.Select(l => l.VariantId).Distinct().Count() != lines.Count)
        {
            throw new ArgumentException("A variant may appear on one line only.", nameof(lines));
        }

        IReadOnlyList<InventoryStock> locked = await store.LockAsync(lines.Select(l => l.VariantId).ToList(), ct);
        Dictionary<Guid, InventoryStock> rows = locked.ToDictionary(s => s.ProductVariantId);

        List<ReservationShortfall> shortfalls = [];
        foreach (ReservationLine line in lines)
        {
            int available = rows.TryGetValue(line.VariantId, out InventoryStock? row) ? row.Available : 0;
            if (line.Quantity > available)
            {
                shortfalls.Add(new ReservationShortfall(line.VariantId, line.Quantity, available));
            }
        }
        if (shortfalls.Count > 0)
        {
            return new ReservationResult(false, shortfalls);
        }

        DateTimeOffset now = clock.UtcNow;
        foreach (ReservationLine line in lines)
        {
            rows[line.VariantId].Reserve(line.Quantity);
            store.AddReservation(InventoryReservation.Create(Guid.CreateVersion7(now), orderId, line.VariantId, line.Quantity, expiresAt, now));
        }
        return new ReservationResult(true, []);
    }

    public async Task CommitAsync(Guid orderId, CancellationToken ct)
    {
        // Lock order (D3): reservations, then stock rows in variant-ID order.
        IReadOnlyList<InventoryReservation> active = await store.LockActiveReservationsAsync(orderId, ct);
        if (active.Count == 0)
        {
            throw new InvalidOperationException($"Order {orderId} has no active reservation to commit.");
        }

        IReadOnlyList<InventoryStock> locked = await store.LockAsync(active.Select(r => r.ProductVariantId).Distinct().ToList(), ct);
        Dictionary<Guid, InventoryStock> rows = locked.ToDictionary(s => s.ProductVariantId);
        foreach (InventoryReservation reservation in active)
        {
            reservation.Commit();
            rows[reservation.ProductVariantId].Commit(reservation.Quantity);
        }
    }

    public async Task<DateTimeOffset?> GetActiveExpiryAsync(Guid orderId, CancellationToken ct)
    {
        // Locks the reservations like the sweep does (orders, then reservations), so a retry and an expiry cannot interleave.
        IReadOnlyList<InventoryReservation> active = await store.LockActiveReservationsAsync(orderId, ct);
        return active.Count == 0 ? null : active.Min(r => r.ExpiresAt);
    }
}
