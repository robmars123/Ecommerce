using Ecommerce.Catalog.Contracts;
using Ecommerce.Identity.Contracts;
using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.Payments.Contracts;

namespace Ecommerce.Orders.Application;

internal sealed class OrderQueryService(
    IOrderReadStore store, IReturnStore returns, ShippingCarriers carriers, IPaymentAttempts payments, IRefundState refunds, IUserReader users,
    IProductReader products)
{
    /// <summary>The admin list (arch 13.2). A customer email that matches no user is an empty page and no Orders query (rule 8).</summary>
    public async Task<AdminOrderListPage> ListForAdminAsync(AdminOrderFilter filter, string? customerEmail, int page, int pageSize, CancellationToken ct)
    {
        AdminOrderFilter effective = filter;
        if (customerEmail is not null)
        {
            Guid? userId = await users.FindIdByEmailAsync(customerEmail, ct);
            if (userId is null)
            {
                return new AdminOrderListPage([], page, pageSize, 0, 0);
            }
            effective = filter with { UserId = userId };
        }

        RecentOrderPage rows = await store.ListForAdminAsync(effective, page, pageSize, ct);
        IReadOnlyDictionary<Guid, UserContact> contacts = await users.GetContactsAsync(rows.Items.Select(r => r.UserId).Distinct().ToList(), ct);
        List<AdminOrderListItem> items = rows.Items
            .Select(r => new AdminOrderListItem(
                r.Id, r.OrderNumber, contacts.TryGetValue(r.UserId, out UserContact? c) ? c.Email : null, r.CreatedAt, r.Status, r.GrandTotal, r.Currency))
            .ToList();
        return new AdminOrderListPage(items, page, pageSize, rows.TotalCount, (rows.TotalCount + pageSize - 1) / pageSize);
    }

    public async Task<OrderListPage> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        OrderListRows rows = await store.ListAsync(userId, page, pageSize, ct);
        List<OrderListItem> items = rows.Items
            .Select(r => new OrderListItem(
                r.Id, r.OrderNumber, r.CreatedAt, r.Status, r.GrandTotal, r.Currency,
                r.CarrierCode is null || r.TrackingNumber is null ? null : carriers.BuildTrackingUrl(r.CarrierCode, r.TrackingNumber)))
            .ToList();
        int totalPages = (rows.TotalCount + pageSize - 1) / pageSize;
        return new OrderListPage(items, page, pageSize, rows.TotalCount, totalPages);
    }

    public async Task<OrderReadResult<OrderDetailView>> GetAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        OrderDetailRow? order = await store.GetAsync(orderId, ct);
        if (order is null)
        {
            return new OrderReadResult<OrderDetailView>.NotFound();
        }
        if (order.UserId != userId)
        {
            return new OrderReadResult<OrderDetailView>.Forbidden();
        }

        return new OrderReadResult<OrderDetailView>.Ok(await BuildAsync(order, ct));
    }

    /// <summary>Any order, for Admin and Support (the route's policy decides who). Null when it does not exist.</summary>
    public async Task<AdminOrderDetailView?> GetForAdminAsync(Guid orderId, CancellationToken ct)
    {
        OrderDetailRow? order = await store.GetAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }

        OrderDetailView detail = await BuildAsync(order, ct);
        return new AdminOrderDetailView(detail, await refunds.GetSummaryAsync(orderId, ct), await returns.ListForOrderAsync(orderId, ct));
    }

    private async Task<OrderDetailView> BuildAsync(OrderDetailRow order, CancellationToken ct)
    {
        List<ShipmentView> shipments = order.Shipments
            .Select(s => new ShipmentView(
                carriers.NameOf(s.Carrier), s.TrackingNumber, carriers.BuildTrackingUrl(s.Carrier, s.TrackingNumber), s.ShippedAt, s.DeliveredAt))
            .ToList();
        PaymentAttemptInfo? latest = await payments.GetLatestAsync(order.Id, ct);
        RefundSummary summary = await refunds.GetSummaryAsync(order.Id, ct);
        IReadOnlyList<VariantSnapshot> snapshots = await products.GetVariantSnapshotsAsync(order.Items.Select(i => i.VariantId).Distinct().ToList(), ct);
        OrderPaymentView? payment = latest is null
            ? null
            : new OrderPaymentView(latest.Method, order.Status == OrderStatus.PendingPayment ? order.GrandTotal : 0m, summary.Captured);
        return new OrderDetailView(order, shipments, latest?.Status, summary.Refunds, snapshots.ToDictionary(s => s.VariantId), payment);
    }
}
