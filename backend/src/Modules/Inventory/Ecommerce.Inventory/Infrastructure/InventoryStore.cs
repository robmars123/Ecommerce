using Ecommerce.Inventory.Contracts;
using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ecommerce.Inventory.Infrastructure;

internal sealed class InventoryStore(EcommerceDbContext db) : IInventoryStore
{
    public async Task EnsureRowAsync(Guid variantId, CancellationToken ct)
    {
        RequireTransaction();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO inventory_stock (product_variant_id, quantity_on_hand, quantity_reserved) VALUES ({variantId}, 0, 0) ON CONFLICT (product_variant_id) DO NOTHING",
            ct);
    }

    public async Task<IReadOnlyList<InventoryStock>> LockAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        RequireTransaction();
        DetachUnchanged<InventoryStock>();
        Guid[] ids = variantIds.ToArray();
        // ORDER BY in SQL, never in C#: uuid and Guid sort differently, and the lock order must match across callers.
        return await db.Set<InventoryStock>()
            .FromSql($"SELECT product_variant_id, quantity_on_hand, quantity_reserved, xmin FROM inventory_stock WHERE product_variant_id = ANY({ids}) ORDER BY product_variant_id FOR UPDATE")
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, StockCounts>> GetAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct)
    {
        List<InventoryStock> rows = await db.Set<InventoryStock>().AsNoTracking()
            .Where(s => variantIds.Contains(s.ProductVariantId))
            .ToListAsync(ct);
        return rows.ToDictionary(s => s.ProductVariantId, s => new StockCounts(s.QuantityOnHand, s.QuantityReserved));
    }

    public async Task<IReadOnlyList<LowStockItem>> ListLowAsync(int threshold, int max, CancellationToken ct) =>
        await LowStock(threshold)
            .OrderBy(s => s.QuantityOnHand - s.QuantityReserved).ThenBy(s => s.ProductVariantId)
            .Take(max)
            .Select(s => new LowStockItem(s.ProductVariantId, s.QuantityOnHand - s.QuantityReserved))
            .ToListAsync(ct);

    public async Task<int> CountLowAsync(int threshold, CancellationToken ct) =>
        await LowStock(threshold).CountAsync(ct);

    // One predicate for the list and the count, so they cannot drift.
    private IQueryable<InventoryStock> LowStock(int threshold) =>
        db.Set<InventoryStock>().AsNoTracking().Where(s => s.QuantityOnHand - s.QuantityReserved <= threshold);

    public async Task<IReadOnlyList<Guid>> FindExpiredOrderIdsAsync(DateTimeOffset now, int max, CancellationToken ct) =>
        await db.Set<InventoryReservation>().AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now)
            .Select(r => r.OrderId)
            .Distinct()
            .OrderBy(id => id)
            .Take(max)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InventoryReservation>> LockActiveReservationsAsync(Guid orderId, CancellationToken ct)
    {
        RequireTransaction();
        DetachUnchanged<InventoryReservation>();
        return await db.Set<InventoryReservation>()
            .FromSql($"SELECT id, order_id, product_variant_id, quantity, status, expires_at, created_at FROM inventory_reservations WHERE order_id = {orderId} AND status = 'Active' ORDER BY product_variant_id FOR UPDATE")
            .ToListAsync(ct);
    }

    public void AddReservation(InventoryReservation reservation) => db.Set<InventoryReservation>().Add(reservation);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Inventory row locks need an open transaction; call inside IUnitOfWork.InTransactionAsync.");
        }
    }

    // A tracked row left over from earlier in this scope would keep its old values; forget unchanged ones so the locked read wins.
    private void DetachUnchanged<T>() where T : class
    {
        foreach (EntityEntry<T> entry in db.ChangeTracker.Entries<T>().Where(e => e.State == EntityState.Unchanged).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
