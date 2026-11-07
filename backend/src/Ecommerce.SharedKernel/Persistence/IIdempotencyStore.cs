namespace Ecommerce.SharedKernel.Persistence;

/// <summary>
/// One stored idempotency key (arch 7.1). <see cref="ResponseJson"/> is the stored reply a repeated key returns
/// (cart-merge); <see cref="ResultRef"/> points at a created row (an order, for checkout).
/// </summary>
public sealed record IdempotencyEntry(
    string Scope,
    string Key,
    Guid? UserId,
    string RequestHash,
    string? ResultRef,
    string? ResponseJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Looks up and stages idempotency keys. <see cref="Add"/> only stages the row, like <c>IAuditLog</c>: it commits or
/// rolls back with the change it protects. A second writer of the same (scope, key) ends in
/// <see cref="UniqueConstraintViolationException"/> at commit.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> FindAsync(string scope, string key, CancellationToken ct);

    void Add(IdempotencyEntry entry);
}
