using System.Text.Json;
using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Outbox;

namespace Ecommerce.Orders.Application;

/// <summary>
/// The one place an order transition becomes an outbox event (Slice 6 D5, Test 3: one transition, one event). It reads the
/// history row the domain just appended, so the event can never disagree with the timeline. OrderPaid is built by the
/// payment handler (it carries the lines the stock commit used); every other transition comes through here.
/// </summary>
internal static class OrderEvents
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static OutboxMessage For(Order order)
    {
        OrderStatusChange change = order.History[^1];
        string type = change.ToStatus == OrderStatus.Shipped ? OrderEventTypes.OrderShipped : OrderEventTypes.OrderStatusChanged;
        OrderEventPayload payload = new(order.Id, order.UserId, change.FromStatus?.ToString() ?? "", change.ToStatus.ToString(), change.CreatedAt);
        return new OutboxMessage(
            Guid.CreateVersion7(change.CreatedAt), type, "Order", order.Id, order.Version, JsonSerializer.Serialize(payload, Json), change.CreatedAt);
    }
}
