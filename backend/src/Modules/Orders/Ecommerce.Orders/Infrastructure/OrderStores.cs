using Ecommerce.Orders.Application;
using Ecommerce.Orders.Contracts;
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
            // Loads the lines and shipments; EF attaches them to the tracked order.
            await db.Set<OrderItem>().Where(i => i.OrderId == id).ToListAsync(ct);
            await db.Set<Shipment>().Where(s => s.OrderId == id).ToListAsync(ct);
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

    public async Task<RecentOrderPage> ListRecentAsync(int page, int pageSize, CancellationToken ct)
    {
        IQueryable<Order> all = db.Set<Order>().AsNoTracking();
        int total = await all.CountAsync(ct);
        List<Order> rows = await all.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new RecentOrderPage(
            rows.Select(o => new RecentOrder(o.Id, o.OrderNumber, o.UserId, o.CreatedAt, o.GrandTotal, o.Currency, o.Status.ToString())).ToList(), total);
    }

    public Task<int> CountPendingReturnsAsync(CancellationToken ct) =>
        db.Set<ReturnRequest>().AsNoTracking().CountAsync(r => r.Status == ReturnStatus.Requested, ct);
}

internal sealed record LineName(Guid OrderId, Guid VariantId, string Name);

internal sealed class ReturnStore(EcommerceDbContext db) : IReturnStore
{
    public void Add(ReturnRequest request) => db.Set<ReturnRequest>().Add(request);

    public async Task<ReturnRequest?> LockAsync(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The return lock needs an open transaction; call inside IUnitOfWork.InTransactionAsync.");
        }
        List<ReturnRequest> rows = await db.Set<ReturnRequest>()
            .FromSql($"SELECT id, order_id, items, reason, status, admin_note, created_at, decided_at FROM return_requests WHERE id = {id} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public Task<ReturnRequest?> GetOpenAsync(Guid orderId, CancellationToken ct) =>
        db.Set<ReturnRequest>().FirstOrDefaultAsync(
            r => r.OrderId == orderId && (r.Status == ReturnStatus.Requested || r.Status == ReturnStatus.Approved), ct);

    public async Task<ReturnRows> ListAsync(ReturnStatus? status, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<ReturnRequest> query = db.Set<ReturnRequest>().AsNoTracking().Where(r => status == null || r.Status == status);
        int total = await query.CountAsync(ct);
        List<ReturnRequest> page1 = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new ReturnRows(await ToRowsAsync(page1, ct), total);
    }

    public async Task<IReadOnlyList<ReturnRow>> ListForOrderAsync(Guid orderId, CancellationToken ct) =>
        await ToRowsAsync(await db.Set<ReturnRequest>().AsNoTracking().Where(r => r.OrderId == orderId).OrderBy(r => r.CreatedAt).ToListAsync(ct), ct);

    public async Task<ReturnRow?> GetRowAsync(Guid id, CancellationToken ct)
    {
        ReturnRequest? request = await db.Set<ReturnRequest>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return request is null ? null : (await ToRowsAsync([request], ct))[0];
    }

    // Order number and line names come from the same module's tables (no cross-module join).
    private async Task<IReadOnlyList<ReturnRow>> ToRowsAsync(List<ReturnRequest> requests, CancellationToken ct)
    {
        Guid[] orderIds = requests.Select(r => r.OrderId).Distinct().ToArray();
        Dictionary<Guid, string> numbers = await db.Set<Order>().AsNoTracking().Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.OrderNumber, ct);
        List<LineName> lines = await db.Set<OrderItem>().AsNoTracking().Where(i => orderIds.Contains(i.OrderId))
            .Select(i => new LineName(i.OrderId, i.ProductVariantId, i.NameSnapshot)).ToListAsync(ct);
        return requests.Select(r => new ReturnRow(
            r.Id, r.OrderId, numbers.GetValueOrDefault(r.OrderId, ""), r.Status, r.Reason, r.AdminNote,
            r.Items.Select(i => new ReturnItemRow(i.VariantId, lines.FirstOrDefault(l => l.OrderId == r.OrderId && l.VariantId == i.VariantId)?.Name ?? "", i.Quantity)).ToList(),
            r.CreatedAt, r.DecidedAt)).ToList();
    }
}

internal sealed class FulfillmentSagaStore(EcommerceDbContext db) : IFulfillmentSagaStore
{
    public Task<FulfillmentSaga?> GetByOrderAsync(Guid orderId, CancellationToken ct) =>
        db.Set<FulfillmentSaga>().FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    public void Add(FulfillmentSaga saga) => db.Set<FulfillmentSaga>().Add(saga);
}
