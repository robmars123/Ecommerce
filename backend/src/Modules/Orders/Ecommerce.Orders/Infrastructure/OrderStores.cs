using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ecommerce.Orders.Infrastructure;

internal sealed class OrderStore(EcommerceDbContext db) : IOrderStore
{
    public Task<Order?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        db.Set<Order>().Include(o => o.Shipments).FirstOrDefaultAsync(o => o.Id == id, ct);

    public void Add(Order order) => db.Set<Order>().Add(order);

    public async Task<Order?> LockForUpdateAsync(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The order lock needs an open transaction; call inside IUnitOfWork.InTransactionAsync.");
        }

        // A tracked row left over from earlier in this scope would keep its old values; forget unchanged ones so the locked read wins.
        foreach (EntityEntry<Order> entry in db.ChangeTracker.Entries<Order>().Where(e => e.State == EntityState.Unchanged).ToList())
        {
            entry.State = EntityState.Detached;
        }
        List<Order> rows = await db.Set<Order>()
            .FromSql($"SELECT id, order_number, idempotency_key, user_id, status, subtotal, discount_total, shipping_total, tax_total, grand_total, currency, shipping_method_code, shipping_method_name, shipping_address_snapshot, version, created_at, updated_at FROM orders WHERE id = {id} FOR UPDATE")
            .ToListAsync(ct);
        Order? order = rows.SingleOrDefault();
        if (order is not null)
        {
            // Loads the lines; EF attaches them to the tracked order.
            await db.Set<OrderItem>().Where(i => i.OrderId == id).ToListAsync(ct);
        }
        return order;
    }

    public Task<long> NextOrderNumberAsync(CancellationToken ct) =>
        db.Database.SqlQuery<long>($"SELECT nextval('order_number_seq') AS \"Value\"").SingleAsync(ct);

    public async Task<Guid?> GetOwnerAsync(Guid id, CancellationToken ct) =>
        await db.Set<Order>().AsNoTracking().Where(o => o.Id == id).Select(o => (Guid?)o.UserId).FirstOrDefaultAsync(ct);
}

internal sealed class OrderReadStore(EcommerceDbContext db) : IOrderReadStore
{
    public async Task<OrderListRows> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<Order> mine = db.Set<Order>().AsNoTracking().Where(o => o.UserId == userId);
        int total = await mine.CountAsync(ct);
        List<OrderListRow> rows = await mine
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderListRow(
                o.Id, o.OrderNumber, o.CreatedAt, o.Status, o.GrandTotal, o.Currency,
                o.Shipments.OrderByDescending(s => s.ShippedAt).Select(s => s.Carrier).FirstOrDefault(),
                o.Shipments.OrderByDescending(s => s.ShippedAt).Select(s => s.TrackingNumber).FirstOrDefault()))
            .ToListAsync(ct);
        return new OrderListRows(rows, total);
    }

    public Task<OrderDetailRow?> GetAsync(Guid orderId, CancellationToken ct) =>
        db.Set<Order>().AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new OrderDetailRow(
                o.Id, o.UserId, o.OrderNumber, o.CreatedAt, o.Status, o.Currency,
                o.Subtotal, o.DiscountTotal, o.ShippingTotal, o.TaxTotal, o.GrandTotal,
                o.ShippingMethodCode, o.ShippingMethodName, o.ShippingAddress,
                o.Items.OrderBy(i => i.Id)
                    .Select(i => new OrderItemRow(i.ProductVariantId, i.SkuSnapshot, i.NameSnapshot, i.UnitPriceSnapshot, i.Quantity, i.LineTotal))
                    .ToList(),
                o.History.OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                    .Select(h => new TimelineRow(h.FromStatus, h.ToStatus, h.CreatedAt, h.Reason))
                    .ToList(),
                o.Shipments.OrderBy(s => s.ShippedAt).ThenBy(s => s.Id)
                    .Select(s => new ShipmentRow(s.Carrier, s.TrackingNumber, s.ShippedAt, s.DeliveredAt))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
}
