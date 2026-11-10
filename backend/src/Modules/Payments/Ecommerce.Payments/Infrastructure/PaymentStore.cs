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

    public async Task<Payment?> LockPayingPaymentAsync(Guid orderId, CancellationToken ct)
    {
        RequireTransaction();
        ForgetUnchangedPayments();
        // The attempt that paid the order: captured, and not the extra payment an automatic refund (no actor) is returning.
        List<Payment> rows = await db.Set<Payment>()
            .FromSql($"""
                SELECT p.id, p.order_id, p.attempt, p.provider, p.provider_payment_id, p.amount, p.currency, p.status, p.created_at, p.updated_at
                FROM payments p
                WHERE p.order_id = {orderId} AND p.status IN ('Succeeded','PartiallyRefunded','Refunded')
                  AND NOT EXISTS (SELECT 1 FROM refunds r WHERE r.payment_id = p.id AND r.actor_id IS NULL)
                ORDER BY p.attempt DESC
                LIMIT 1
                FOR UPDATE OF p
                """)
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public async Task<Payment?> LockByIdAsync(Guid paymentId, CancellationToken ct)
    {
        RequireTransaction();
        ForgetUnchangedPayments();
        List<Payment> rows = await db.Set<Payment>()
            .FromSql($"SELECT id, order_id, attempt, provider, provider_payment_id, amount, currency, status, created_at, updated_at FROM payments WHERE id = {paymentId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public Task<Payment?> GetPayingAsync(Guid orderId, CancellationToken ct) =>
        db.Set<Payment>().AsNoTracking()
            .Where(p => p.OrderId == orderId
                        && (p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.PartiallyRefunded || p.Status == PaymentStatus.Refunded)
                        && !db.Set<Refund>().Any(r => r.PaymentId == p.Id && r.ActorId == null))
            .OrderByDescending(p => p.Attempt)
            .FirstOrDefaultAsync(ct);

    public async Task<decimal> SumRefundsAsync(
        Guid paymentId, IReadOnlyCollection<RefundStatus> statuses, IReadOnlyCollection<Guid> except, CancellationToken ct) =>
        await db.Set<Refund>().AsNoTracking()
            .Where(r => r.PaymentId == paymentId && statuses.Contains(r.Status) && !except.Contains(r.Id))
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;

    public async Task<IReadOnlyList<Refund>> ListRefundsAsync(Guid paymentId, CancellationToken ct) =>
        await db.Set<Refund>().AsNoTracking().Where(r => r.PaymentId == paymentId).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync(ct);

    public async Task<OrderRefundRows> ListRefundsForOrdersAsync(IReadOnlyCollection<Guid> orderIds, int recentTake, CancellationToken ct)
    {
        IQueryable<Refund> refunds = db.Set<Refund>().AsNoTracking()
            .Where(r => db.Set<Payment>().Any(p => p.Id == r.PaymentId && orderIds.Contains(p.OrderId)));
        int count = await refunds.CountAsync(ct);
        decimal actorTotal = await refunds.Where(r => r.Status == RefundStatus.Succeeded && r.ActorId != null).SumAsync(r => r.Amount, ct);
        List<Refund> recent = await refunds.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(recentTake).ToListAsync(ct);
        List<Guid> paymentIds = recent.Select(r => r.PaymentId).Distinct().ToList();
        Dictionary<Guid, Guid> orderOf = await db.Set<Payment>().AsNoTracking()
            .Where(p => paymentIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.OrderId, ct);
        return new OrderRefundRows(actorTotal, count, recent.Select(r => new OrderRefundRow(r, orderOf[r.PaymentId])).ToList());
    }

    public Task<Refund?> FindRefundAsync(Guid? refundId, string? providerRefundId, CancellationToken ct) =>
        db.Set<Refund>().AsNoTracking()
            .Where(r => (refundId != null && r.Id == refundId) || (providerRefundId != null && r.ProviderRefundId == providerRefundId))
            .FirstOrDefaultAsync(ct);

    public Task<Refund?> GetRefundTrackedAsync(Guid refundId, CancellationToken ct) =>
        db.Set<Refund>().FirstOrDefaultAsync(r => r.Id == refundId, ct);

    public async Task SetProviderRefundIdAsync(Guid refundId, string providerRefundId, CancellationToken ct) =>
        await db.Database.ExecuteSqlAsync(
            $"UPDATE refunds SET provider_refund_id = {providerRefundId} WHERE id = {refundId} AND provider_refund_id IS NULL", ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The payment lock needs an open transaction; call inside IUnitOfWork.InTransactionAsync.");
        }
    }

    // A tracked row left over from earlier in this scope would keep its old values; forget unchanged ones so the locked read wins.
    private void ForgetUnchangedPayments()
    {
        foreach (EntityEntry<Payment> entry in db.ChangeTracker.Entries<Payment>().Where(e => e.State == EntityState.Unchanged).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
