using System.Text.Json;
using Ecommerce.Identity.Contracts;
using Ecommerce.Notifications.Contracts;
using Ecommerce.Orders.Contracts;
using Ecommerce.SharedKernel.Outbox;

namespace Ecommerce.Notifications.Application;

/// <summary>
/// OrderPaid sends the confirmation, OrderShipped the carrier and tracking (arch 8.4, 8.6). The send happens with no database
/// transaction open (the outbox runtime guarantees it); the idempotency key "{consumer}:{eventId}" lets the provider drop a
/// repeat, so a crash after the send never mails twice. A sender that throws makes the runtime retry, then dead-letter.
/// </summary>
internal sealed class OrderEmailConsumer(IUserReader users, IOrderReader orders, IEmailSender sender) : IOutboxConsumer
{
    public const string ConsumerName = "notifications.order-email";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => ConsumerName;

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string> { OrderEventTypes.OrderPaid, OrderEventTypes.OrderShipped };

    public async Task HandleAsync(OutboxMessage message, CancellationToken ct)
    {
        OrderEventPayload payload = JsonSerializer.Deserialize<OrderEventPayload>(message.PayloadJson, Json)!;
        UserContact? contact = await users.GetContactAsync(payload.UserId, ct);
        OrderNotificationView? order = await orders.GetNotificationViewAsync(payload.OrderId, ct);
        if (contact is null || order is null)
        {
            // The recipient or the order no longer exists: there is nobody to tell, and retrying cannot change that.
            return;
        }

        (string subject, string body) = message.EventType == OrderEventTypes.OrderShipped
            ? EmailTemplates.OrderShipped(contact.DisplayName, order)
            : EmailTemplates.OrderConfirmation(contact.DisplayName, order);
        await sender.SendAsync(new EmailMessage(contact.Email, subject, body, $"{Name}:{message.EventId}"), ct);
    }
}
