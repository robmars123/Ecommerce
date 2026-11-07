using Ecommerce.Cart.Application;
using Ecommerce.Cart.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using CartAggregate = Ecommerce.Cart.Domain.Cart;

namespace Ecommerce.Cart.Infrastructure;

internal sealed class CartStore(EcommerceDbContext db) : ICartStore
{
    public async Task<CartAggregate> EnsureAndLockAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The cart lock needs an open transaction; call inside IUnitOfWork.InTransactionAsync.");
        }

        // Two first writes by one user race on the unique user_id; the loser does nothing here and then waits on the lock.
        Guid newId = Guid.CreateVersion7(now);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO carts (id, user_id, updated_at) VALUES ({newId}, {userId}, {now}) ON CONFLICT (user_id) DO NOTHING", ct);
        CartAggregate cart = await db.Set<CartAggregate>()
            .FromSql($"SELECT id, user_id, updated_at FROM carts WHERE user_id = {userId} FOR UPDATE")
            .SingleAsync(ct);
        // Loads the lines; EF attaches them to the tracked cart.
        await db.Set<CartItem>().Where(i => i.CartId == cart.Id).ToListAsync(ct);
        return cart;
    }

    public Task<CartAggregate?> GetAsync(Guid userId, CancellationToken ct) =>
        db.Set<CartAggregate>().AsNoTracking().Include(c => c.Items).SingleOrDefaultAsync(c => c.UserId == userId, ct);
}
