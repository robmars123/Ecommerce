using System.Text.Json;
using Ecommerce.Admin.Domain;
using Ecommerce.Orders.Contracts;
using Ecommerce.Payments.Contracts;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Projections;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Admin.Infrastructure;

/// <summary>
/// The admin dashboard read model (arch 8.8, Slice 6b D8, D9): one row per order and one per actor-requested refund, built only from
/// outbox payloads, never from today's order state, so a replay gives the same rows. Order events are version-gated by the runtime.
/// </summary>
internal sealed class DashboardProjector(EcommerceDbContext db) : IProjector
{
    public const string ProjectorName = "admin.dashboard";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => ProjectorName;

    public IReadOnlySet<string> EventTypes { get; } = new HashSet<string>
    {
        OrderEventTypes.OrderPaid, OrderEventTypes.OrderShipped, OrderEventTypes.OrderStatusChanged, PaymentEventTypes.RefundSucceeded,
    };

    public IReadOnlySet<string> VersionedAggregateTypes { get; } = new HashSet<string> { "Order" };

    public async Task ApplyAsync(OutboxMessage message, CancellationToken ct)
    {
        switch (message.EventType)
        {
            case OrderEventTypes.OrderPaid:
                OrderPaidPayload paid = JsonSerializer.Deserialize<OrderPaidPayload>(message.PayloadJson, Json)!;
                DashboardOrder paidRow = await FindOrAddAsync(paid.OrderId, paid.UserId, ct);
                paidRow.Status = paid.ToStatus;
                paidRow.GrandTotal = paid.GrandTotal;
                paidRow.Currency = paid.Currency;
                paidRow.PaidAt = paid.OccurredAt;
                break;
            case PaymentEventTypes.RefundSucceeded:
                RefundSucceededPayload refund = JsonSerializer.Deserialize<RefundSucceededPayload>(message.PayloadJson, Json)!;
                if (await db.Set<DashboardRefund>().FindAsync([refund.RefundId], ct) is null)
                {
                    db.Set<DashboardRefund>().Add(new DashboardRefund
                    {
                        RefundId = refund.RefundId, OrderId = refund.OrderId, Amount = refund.Amount, SucceededAt = refund.SucceededAt,
                    });
                }
                break;
            default:
                OrderEventPayload changed = JsonSerializer.Deserialize<OrderEventPayload>(message.PayloadJson, Json)!;
                (await FindOrAddAsync(changed.OrderId, changed.UserId, ct)).Status = changed.ToStatus;
                break;
        }
    }

    public async Task ResetAsync(CancellationToken ct)
    {
        await db.Set<DashboardOrder>().ExecuteDeleteAsync(ct);
        await db.Set<DashboardRefund>().ExecuteDeleteAsync(ct);
    }

    private async Task<DashboardOrder> FindOrAddAsync(Guid orderId, Guid userId, CancellationToken ct)
    {
        DashboardOrder? row = await db.Set<DashboardOrder>().FindAsync([orderId], ct);
        if (row is not null)
        {
            return row;
        }
        row = new DashboardOrder { OrderId = orderId, UserId = userId };
        db.Set<DashboardOrder>().Add(row);
        return row;
    }
}
