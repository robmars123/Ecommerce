using System.Text.Json;
using Ecommerce.Orders.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.Payments.Contracts;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

internal sealed record SagaView(Guid SagaId, string Status, decimal RefundAmount, string Currency);

/// <summary>Ok | NotFound | Conflict (the order is not in a status that can fail fulfillment).</summary>
internal abstract record SagaResult
{
    private SagaResult() { }

    public sealed record Ok(SagaView Value) : SagaResult;

    public sealed record NotFound : SagaResult;

    public sealed record Conflict(string Message) : SagaResult;
}

/// <summary>
/// Marks an order unfulfillable (arch 8.8.4, Slice 6b D17). ONE transaction: lock the order, return the order's existing saga if it has
/// one (a repeat is not an error), else check the status, fix the refund amount from what is still refundable (read after the order
/// lock, Slice 6 D8), and write the saga, the audit row and the FulfillmentFailed event together. The refund itself is the payments
/// consumer's job: this request never calls Payments' refund path, so the lock order stays payments, then orders.
/// </summary>
internal sealed class FulfillmentSagaService(
    IOrderStore orders,
    IFulfillmentSagaStore sagas,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IOutbox outbox,
    IRefundState refunds,
    IClock clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record BeforeState(string Status, int Version);

    private sealed record AfterState(string Status, int Version, Guid SagaId, string SagaStatus, decimal RefundAmount);

    public Task<SagaResult> StartAsync(Guid actorId, Guid orderId, string reason, CancellationToken ct) =>
        unitOfWork.InTransactionAsync(c => RunAsync(actorId, orderId, reason, c), ct);

    private async Task<SagaResult> RunAsync(Guid actorId, Guid orderId, string reason, CancellationToken ct)
    {
        Order? order = await orders.LockForUpdateAsync(orderId, ct);
        if (order is null)
        {
            return new SagaResult.NotFound();
        }
        if (await sagas.GetByOrderAsync(orderId, ct) is { } existing)
        {
            return new SagaResult.Ok(new SagaView(existing.Id, existing.Status, existing.RefundAmount, order.Currency));
        }
        if (order.Status is not (OrderStatus.Paid or OrderStatus.Processing))
        {
            return new SagaResult.Conflict($"An order that is {order.Status} cannot be marked unfulfillable.");
        }

        DateTimeOffset now = clock.UtcNow;
        RefundSummary summary = await refunds.GetSummaryAsync(orderId, ct);
        FulfillmentSaga saga = FulfillmentSaga.Start(orderId, actorId, reason, summary.Remaining, now);
        sagas.Add(saga);
        audit.Add(new AuditEntry(
            actorId, "fulfillment.unfulfillable", "order", orderId,
            JsonSerializer.Serialize(new BeforeState(order.Status.ToString(), order.Version), Json),
            JsonSerializer.Serialize(new AfterState(order.Status.ToString(), order.Version, saga.Id, saga.Status, saga.RefundAmount), Json),
            reason, now));
        outbox.Add(new OutboxMessage(
            Guid.CreateVersion7(now), FulfillmentEventTypes.FulfillmentFailed, "FulfillmentSaga", saga.Id, 1,
            JsonSerializer.Serialize(new FulfillmentFailedPayload(saga.Id, orderId, actorId, saga.RefundAmount, order.Currency, reason, now), Json), now));
        return new SagaResult.Ok(new SagaView(saga.Id, saga.Status, saga.RefundAmount, order.Currency));
    }
}
