namespace Ecommerce.SharedKernel.Outbox;

/// <summary>
/// One outbox event (arch 8.4). <see cref="AggregateVersion"/> is the order's version for order events, else 1.
/// The payload is internal until the consumers arrive (Slice 6).
/// </summary>
public sealed record OutboxMessage(
    Guid EventId,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    int AggregateVersion,
    string PayloadJson,
    DateTimeOffset OccurredAt);

/// <summary>
/// Stages an outbox row in the current scope's unit of work. It does not save: the row commits or rolls back with the
/// state change it describes (rule 6), exactly like <c>IAuditLog</c>.
/// </summary>
public interface IOutbox
{
    void Add(OutboxMessage message);
}
