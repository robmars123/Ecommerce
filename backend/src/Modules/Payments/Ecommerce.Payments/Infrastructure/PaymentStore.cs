using Ecommerce.Payments.Application;
using Ecommerce.Payments.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ecommerce.Payments.Infrastructure;

internal sealed class PaymentStore(EcommerceDbContext db) : IPaymentStore
{
    public void Add(Payment payment) => db.Set<Payment>().Add(payment);

    public void AddRefund(Refund refund) => db.Set<Refund>().Add(refund);

    public Task<Payment?> GetAsync(Guid paymentId, CancellationToken ct) =>
        db.Set<Payment>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public Task<Payment?> GetLatestAsync(Guid orderId, CancellationToken ct) =>
        db.Set<Payment>().AsNoTracking().Where(p => p.OrderId == orderId).OrderByDescending(p => p.Attempt).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListOpenIntentsAsync(Guid orderId, Guid exceptPaymentId, CancellationToken ct) =>
        await db.Set<Payment>().AsNoTracking()
            .Where(p => p.OrderId == orderId && p.Id != exceptPaymentId && p.ProviderPaymentId != null
                        && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Failed))
            .OrderBy(p => p.Attempt)
            .ToListAsync(ct);

    public async Task<Payment?> LockByProviderIdAsync(string providerPaymentId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The payment lock needs an open transaction; call inside IUnitOfWork.InTransactionAsync.");
        }

        // A tracked row left over from earlier in this scope would keep its old values; forget unchanged ones so the locked read wins.
        foreach (EntityEntry<Payment> entry in db.ChangeTracker.Entries<Payment>().Where(e => e.State == EntityState.Unchanged).ToList())
        {
            entry.State = EntityState.Detached;
        }
        List<Payment> rows = await db.Set<Payment>()
            .FromSql($"SELECT id, order_id, attempt, provider, provider_payment_id, amount, currency, status, created_at, updated_at FROM payments WHERE provider_payment_id = {providerPaymentId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public async Task<string?> AttachProviderIdAsync(Guid paymentId, string providerPaymentId, DateTimeOffset now, CancellationToken ct)
    {
        // Targeted UPDATE: never rewrites a status the webhook may have just changed, and never overwrites a stored id.
        await db.Database.ExecuteSqlAsync(
            $"UPDATE payments SET provider_payment_id = {providerPaymentId}, updated_at = {now} WHERE id = {paymentId} AND provider_payment_id IS NULL", ct);
        return await db.Set<Payment>().AsNoTracking().Where(p => p.Id == paymentId).Select(p => p.ProviderPaymentId).FirstOrDefaultAsync(ct);
    }
}
