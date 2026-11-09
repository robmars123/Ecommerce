using System.Text.Json;
using System.Text.Json.Serialization;
using Ecommerce.Orders.Domain;
using Ecommerce.Payments.Contracts;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

/// <summary>
/// The admin fulfillment actions (arch 8.6). Each one runs in one transaction that locks the order FOR UPDATE, lets the domain
/// decide, and writes the status, history row, shipment, audit row and the order's one outbox event together or not at all.
/// process and ship also read the order's refunds (no payment lock, D8) and refuse while a full refund is Pending (arch 8.5 step 2).
/// </summary>
internal sealed class OrderFulfillmentService(
    IOrderStore store,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IOutbox outbox,
    IRefundState refunds,
    IFulfillmentSagaStore sagas,
    IClock clock,
    ShippingCarriers carriers)
{
    private sealed record AuditState(
        string Status,
        int Version,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Carrier = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TrackingNumber = null);

    public Task<FulfillmentResult> ProcessAsync(Guid actorId, Guid orderId, CancellationToken ct) =>
        RunAsync("order.process", actorId, orderId, null, true, (o, now) => o.Process(actorId, now), ct);

    public async Task<FulfillmentResult> ShipAsync(Guid actorId, Guid orderId, string carrierCode, string trackingNumber, CancellationToken ct)
    {
        if (!carriers.Exists(carrierCode))
        {
            return new FulfillmentResult(FulfillmentOutcome.Invalid, "carrierCode", "Choose one of the configured carriers.");
        }
        return await RunAsync("order.ship", actorId, orderId, null, true, (o, now) => o.Ship(actorId, carrierCode, trackingNumber, now), ct);
    }

    public Task<FulfillmentResult> DeliverAsync(Guid actorId, Guid orderId, CancellationToken ct) =>
        RunAsync("order.deliver", actorId, orderId, null, false, (o, now) => o.Deliver(actorId, now), ct);

    public Task<FulfillmentResult> OverrideAsync(Guid actorId, Guid orderId, OrderStatus to, string reason, CancellationToken ct) =>
        RunAsync("order.override", actorId, orderId, reason, false, (o, now) => o.Override(actorId, to, reason, now), ct);

    private async Task<FulfillmentResult> RunAsync(
        string action, Guid actorId, Guid orderId, string? reason, bool refundGuard, Action<Order, DateTimeOffset> apply, CancellationToken ct)
    {
        try
        {
            return await unitOfWork.InTransactionAsync(c => ApplyAsync(action, actorId, orderId, reason, refundGuard, apply, c), ct);
        }
        catch (ConcurrencyConflictException)
        {
            return new FulfillmentResult(FulfillmentOutcome.Conflict, null, "The order was changed by someone else. Reload and try again.");
        }
    }

    private async Task<FulfillmentResult> ApplyAsync(
        string action, Guid actorId, Guid orderId, string? reason, bool refundGuard, Action<Order, DateTimeOffset> apply, CancellationToken ct)
    {
        Order? order = await store.LockForUpdateAsync(orderId, ct);
        if (order is null)
        {
            return new FulfillmentResult(FulfillmentOutcome.NotFound);
        }
        if (action == "order.ship" && await sagas.GetByOrderAsync(orderId, ct) is not null)
        {
            // Marked unfulfillable: its refund is owed, so nothing may leave the building (Slice 6b D18).
            return new FulfillmentResult(FulfillmentOutcome.Conflict, null, "order-unfulfillable: this order cannot be shipped; it is being refunded.");
        }
        if (refundGuard && await refunds.HasPendingFullRefundAsync(orderId, ct))
        {
            return new FulfillmentResult(FulfillmentOutcome.Conflict, null, "A full refund is pending on this order.");
        }

        DateTimeOffset now = clock.UtcNow;
        string before = JsonSerializer.Serialize(new AuditState(order.Status.ToString(), order.Version));
        try
        {
            apply(order, now);
        }
        catch (DomainException ex)
        {
            return new FulfillmentResult(FulfillmentOutcome.Conflict, null, ex.Message);
        }

        Shipment? shipment = action == "order.ship" ? order.Shipments[^1] : null;
        string after = JsonSerializer.Serialize(new AuditState(order.Status.ToString(), order.Version, shipment?.Carrier, shipment?.TrackingNumber));
        audit.Add(new AuditEntry(actorId, action, "order", order.Id, before, after, reason, now));
        outbox.Add(OrderEvents.For(order));
        return new FulfillmentResult(FulfillmentOutcome.Ok);
    }
}
