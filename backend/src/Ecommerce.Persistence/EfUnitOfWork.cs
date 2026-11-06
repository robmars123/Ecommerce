using Ecommerce.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Ecommerce.Persistence;

/// <summary>
/// Scoped unit of work over the one <see cref="EcommerceDbContext"/>. Translates EF/Npgsql failures into the
/// SharedKernel exceptions so Application code never sees EF types.
/// </summary>
internal sealed class EfUnitOfWork(EcommerceDbContext db) : IUnitOfWork
{
    private const string UniqueViolation = "23505";

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (Translate(ex) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("A transaction is already open; nested InTransactionAsync is not supported.");
        }

        // The execution strategy is the default (no retrying strategy), so a manual transaction is allowed.
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            T? result = await work(ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception ex)
        {
            await RollbackQuietlyAsync(tx);
            // Entities from the failed attempt must not leak into a later use of this scope.
            db.ChangeTracker.Clear();
            if (Translate(ex) is { } translated)
            {
                throw translated;
            }
            throw;
        }
    }

    private static async Task RollbackQuietlyAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx)
    {
        try
        {
            await tx.RollbackAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // The connection may already be broken; the original exception is the one that matters.
        }
    }

    private static Exception? Translate(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException c => new ConcurrencyConflictException("The row was changed by someone else.", c),
        DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } pg } u =>
            new UniqueConstraintViolationException(pg.ConstraintName ?? "", u),
        PostgresException { SqlState: UniqueViolation } pg2 => new UniqueConstraintViolationException(pg2.ConstraintName ?? "", pg2),
        _ => null,
    };
}
