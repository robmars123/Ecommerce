using System.Text.Json;
using Ecommerce.Cart.Contracts;
using Ecommerce.Inventory.Contracts;
using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.Payments.Contracts;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

/// <summary>
/// Orders' side of the webhook and of expiry (D3, D8, D11). Both run inside the caller transaction and lock the order
/// FOR UPDATE first. Lock order below the order: reservations, stock rows (variant-ID order), then the cart.
/// </summary>
internal sealed class OrderPaymentHandler(
    IOrderStore orders,
    IInventoryReserver reserver,
    ICartWriter cart,
    IPaymentAttempts payments,
    IOutbox outbox,
    IClock clock) : IOrderPayments, IOrderExpiry
{
    public const string OrderPaid = "OrderPaid";

    private sealed record OrderPaidLine(Guid VariantId, int Quantity);

    private sealed record OrderPaidPayload(
        Guid OrderId, string OrderNumber, Guid UserId, decimal GrandTotal, string Currency, IReadOnlyList<OrderPaidLine> Items);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<MarkPaidOutcome> TryMarkPaidAsync(Guid orderId, CancellationToken ct)
    {
        Order? order = await orders.LockForUpdateAsync(orderId, ct);
        if (order is null)
        {
            return new MarkPaidOutcome(false, "NotFound");
        }
        if (order.Status != OrderStatus.PendingPayment)
        {
            return new MarkPaidOutcome(false, order.Status.ToString());
        }

        DateTimeOffset now = clock.UtcNow;
        order.MarkPaid(now);
        await reserver.CommitAsync(order.Id, ct);
        await cart.RemoveLinesAsync(order.UserId, order.Items.Select(i => i.ProductVariantId).ToList(), ct);
        outbox.Add(new OutboxMessage(
            Guid.CreateVersion7(now), OrderPaid, "Order", order.Id, order.Version,
            JsonSerializer.Serialize(
                new OrderPaidPayload(
                    order.Id, order.OrderNumber, order.UserId, order.GrandTotal, order.Currency,
                    order.Items.Select(i => new OrderPaidLine(i.ProductVariantId, i.Quantity)).ToList()),
                Json),
            now));
        return new MarkPaidOutcome(true, order.Status.ToString());
    }

    public async Task<bool> CancelExpiredAsync(Guid orderId, CancellationToken ct)
    {
        Order? order = await orders.LockForUpdateAsync(orderId, ct);
        if (order is null || order.Status != OrderStatus.PendingPayment)
        {
            return false;
        }

        order.CancelForExpiry(clock.UtcNow);
        await payments.RequestCancelOpenIntentAsync(order.Id, ct);
        return true;
    }
}
