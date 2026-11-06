namespace Ecommerce.SharedKernel.Persistence;

/// <summary>
/// The transaction boundary application services use. Application code cannot see EF, so locking flows
/// run inside <see cref="InTransactionAsync{T}"/>. One transaction per request scope: a nested call throws.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Begins a READ COMMITTED transaction, runs <paramref name="work"/>, saves and commits. On any exception the
    /// transaction is rolled back and the (translated) exception is rethrown.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
