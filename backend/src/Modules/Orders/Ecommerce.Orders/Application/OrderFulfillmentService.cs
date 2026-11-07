using System.Text.Json;
using System.Text.Json.Serialization;
using Ecommerce.Orders.Domain;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

/// <summary>
/// The admin fulfillment actions (arch 8.6). Each one loads the order, lets the domain decide, writes one audit row
/// and saves once, so the status, history row, shipment and audit row commit together or not at all.
/// </summary>
internal sealed class OrderFulfillmentService(
    IOrderStore store,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IClock clock,
    ShippingCarriers carriers)
{
    private sealed record AuditState(
        string Status,
        int Version,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Carrier = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TrackingNumber = null);

    public Task<FulfillmentResult> ProcessAsync(Guid actorId, Guid orderId, CancellationToken ct) =>
        RunAsync("order.process", actorId, orderId, null, (o, now) => o.Process(actorId, now), ct);

    public async Task<FulfillmentResult> ShipAsync(Guid actorId, Guid orderId, string carrierCode, string trackingNumber, CancellationToken ct)
    {
        if (!carriers.Exists(carrierCode))
        {
            return new FulfillmentResult(FulfillmentOutcome.Invalid, "carrierCode", "Choose one of the configured carriers.");
        }
        return await RunAsync("order.ship", actorId, orderId, null, (o, now) => o.Ship(actorId, carrierCode, trackingNumber, now), ct);
    }

    public Task<FulfillmentResult> DeliverAsync(Guid actorId, Guid orderId, CancellationToken ct) =>
        RunAsync("order.deliver", actorId, orderId, null, (o, now) => o.Deliver(actorId, now), ct);

    public Task<FulfillmentResult> OverrideAsync(Guid actorId, Guid orderId, OrderStatus to, string reason, CancellationToken ct) =>
        RunAsync("order.override", actorId, orderId, reason, (o, now) => o.Override(actorId, to, reason, now), ct);

    private async Task<FulfillmentResult> RunAsync(
        string action, Guid actorId, Guid orderId, string? reason, Action<Order, DateTimeOffset> apply, CancellationToken ct)
    {
        Order? order = await store.GetForUpdateAsync(orderId, ct);
        if (order is null)
        {
            return new FulfillmentResult(FulfillmentOutcome.NotFound);
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

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return new FulfillmentResult(FulfillmentOutcome.Conflict, null, "The order was changed by someone else. Reload and try again.");
        }

        return new FulfillmentResult(FulfillmentOutcome.Ok);
    }
}
