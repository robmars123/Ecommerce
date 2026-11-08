namespace Ecommerce.SharedKernel.Outbox;

/// <summary>
/// A side-effect consumer of outbox events (arch 8.4). The runtime in Persistence calls <see cref="HandleAsync"/> with no
/// database transaction open, then records the outcome per (<see cref="Name"/>, event id). A throw means retry. Handlers must
/// be safe to run twice: pass the message's event id to the receiver as its idempotency key.
/// </summary>
public interface IOutboxConsumer
{
    /// <summary>Stable and unique; it is the consumer's key in <c>processed_events</c>.</summary>
    string Name { get; }

    IReadOnlySet<string> EventTypes { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken ct);
}
