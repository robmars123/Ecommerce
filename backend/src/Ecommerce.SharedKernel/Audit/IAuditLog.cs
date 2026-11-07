namespace Ecommerce.SharedKernel.Audit;

/// <summary>One audited action. Before/after are JSON text built by the caller; never put secrets in them.</summary>
public sealed record AuditEntry(
    Guid? ActorId,
    string Action,
    string EntityType,
    Guid EntityId,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    DateTimeOffset At);

/// <summary>
/// Adds an audit row to the current scope's unit of work. It does not save: the row commits or rolls back with the
/// change it describes (rule 13).
/// </summary>
public interface IAuditLog
{
    void Add(AuditEntry entry);
}
