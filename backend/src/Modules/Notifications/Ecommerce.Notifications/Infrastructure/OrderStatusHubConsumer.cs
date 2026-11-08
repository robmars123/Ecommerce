using System.Text.Json;
using Ecommerce.Notifications.Api;
using Ecommerce.Orders.Contracts;
using Ecommerce.SharedKernel.Outbox;
using Microsoft.AspNetCore.SignalR;

namespace Ecommerce.Notifications.Infrastructure;

internal sealed record OrderStatusChangedMessage(Guid OrderId, string Status);

/// <summary>Pushes every order transition to the owner's group (D16). A hint only: with nobody connected it is a no-op.</summary>
internal sealed class OrderStatusHubConsumer(IHubContext<OrderStatusHub> hub) : IOutboxConsumer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => "notifications.order-status-hub";

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string>
    {
        OrderEventTypes.OrderPaid, OrderEventTypes.OrderShipped, OrderEventTypes.OrderStatusChanged,
    };

    public Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        OrderEventPayload payload = JsonSerializer.Deserialize<OrderEventPayload>(message.PayloadJson, Json)!;
        return hub.Clients.Group(OrderStatusHub.GroupOf(payload.UserId))
            .SendAsync(OrderStatusHub.StatusChanged, new OrderStatusChangedMessage(payload.OrderId, payload.ToStatus), ct);
    }
}
