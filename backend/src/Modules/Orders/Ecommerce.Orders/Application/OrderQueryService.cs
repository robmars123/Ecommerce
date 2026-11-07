using Ecommerce.Payments.Contracts;

namespace Ecommerce.Orders.Application;

internal sealed class OrderQueryService(IOrderReadStore store, ShippingCarriers carriers, IPaymentAttempts payments)
{
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

        List<ShipmentView> shipments = order.Shipments
            .Select(s => new ShipmentView(
                carriers.NameOf(s.Carrier), s.TrackingNumber, carriers.BuildTrackingUrl(s.Carrier, s.TrackingNumber), s.ShippedAt, s.DeliveredAt))
            .ToList();
        PaymentAttemptInfo? latest = await payments.GetLatestAsync(orderId, ct);
        return new OrderReadResult<OrderDetailView>.Ok(new OrderDetailView(order, shipments, latest?.Status));
    }
}
