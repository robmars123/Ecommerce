using Ecommerce.Orders.Contracts;

namespace Ecommerce.Orders.Application;

/// <summary>Orders' read for Notifications (D7): the order's own snapshot lines and its latest shipment, carrier name and tracking URL resolved from configuration.</summary>
internal sealed class OrderReader(IOrderReadStore store, ShippingCarriers carriers) : IOrderReader
{
    public async Task<OrderNotificationView?> GetNotificationViewAsync(Guid orderId, CancellationToken ct)
    {
        OrderDetailRow? order = await store.GetAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }

        ShipmentRow? latest = order.Shipments.OrderByDescending(s => s.ShippedAt).FirstOrDefault();
        return new OrderNotificationView(
            order.OrderNumber, order.Currency, order.GrandTotal,
            order.Items.Select(i => new OrderNotificationLine(i.Name, i.Quantity, i.UnitPrice)).ToList(),
            latest is null
                ? null
                : new OrderNotificationShipment(carriers.NameOf(latest.Carrier), latest.TrackingNumber, carriers.BuildTrackingUrl(latest.Carrier, latest.TrackingNumber)));
    }

    public Task<RecentOrderPage> ListRecentAsync(int page, int pageSize, CancellationToken ct) => store.ListRecentAsync(page, pageSize, ct);

    public Task<int> CountPendingReturnsAsync(CancellationToken ct) => store.CountPendingReturnsAsync(ct);

    public Task<CustomerOrders> GetCustomerOrdersAsync(Guid userId, int recentTake, CancellationToken ct) => store.GetCustomerOrdersAsync(userId, recentTake, ct);
}
