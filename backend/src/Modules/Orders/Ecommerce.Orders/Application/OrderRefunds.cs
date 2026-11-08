using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

/// <summary>
/// Orders' side of refunds (D7, D13). Runs in the caller's transaction after Payments holds the payment row (lock order
/// payments, orders). A full refund moves the order through the same state table as every other transition and writes the
/// one OrderStatusChanged event; the order's totals are never touched.
/// </summary>
internal sealed class OrderRefunds(IOrderStore orders, IReturnStore returns, IOutbox outbox, IClock clock) : IOrderRefunds
{
    public async Task<RefundOrderInfo?> LockForRefundAsync(Guid orderId, CancellationToken ct)
    {
        Order? order = await orders.LockForUpdateAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }
        ReturnRequest? open = await returns.GetOpenAsync(orderId, ct);
        return new RefundOrderInfo(order.Status.ToString(), order.UserId, open is { Status: ReturnStatus.Approved });
    }

    public async Task<bool> ApplyFullRefundAsync(Guid orderId, Guid refundId, Guid? actorId, CancellationToken ct)
    {
        Order? order = await orders.LockForUpdateAsync(orderId, ct);
        DateTimeOffset now = clock.UtcNow;
        switch (order?.Status)
        {
            case OrderStatus.Paid or OrderStatus.Processing:
                order.RefundInFull(actorId, now);
                break;
            case OrderStatus.Delivered:
                order.ReturnInFull(actorId, now);
                break;
            default:
                return false;
        }

        outbox.Add(OrderEvents.For(order));
        (await returns.GetOpenAsync(orderId, ct))?.MarkRefunded(now);
        return true;
    }

    public async Task MarkReturnRefundedAsync(Guid orderId, CancellationToken ct) =>
        (await returns.GetOpenAsync(orderId, ct))?.MarkRefunded(clock.UtcNow);
}
