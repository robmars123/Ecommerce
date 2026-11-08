using Ecommerce.Payments.Contracts;
using Ecommerce.Payments.Domain;

namespace Ecommerce.Payments.Application;

/// <summary>Read side for Orders (D7, D8): no locks. The order screens show what the server computed, never a client sum.</summary>
internal sealed class RefundState(IPaymentStore store) : IRefundState
{
    public async Task<bool> HasPendingFullRefundAsync(Guid orderId, CancellationToken ct)
    {
        Payment? payment = await store.GetPayingAsync(orderId, ct);
        if (payment is null)
        {
            return false;
        }
        IReadOnlyCollection<Guid> none = [];
        decimal pending = await store.SumRefundsAsync(payment.Id, [RefundStatus.Pending], none, ct);
        decimal succeeded = await store.SumRefundsAsync(payment.Id, [RefundStatus.Succeeded], none, ct);
        return pending > 0m && pending + succeeded >= payment.Amount;
    }

    public async Task<RefundSummary> GetSummaryAsync(Guid orderId, CancellationToken ct)
    {
        Payment? payment = await store.GetPayingAsync(orderId, ct);
        if (payment is null)
        {
            return new RefundSummary(null, 0m, 0m, 0m, 0m, []);
        }
        IReadOnlyList<Refund> refunds = await store.ListRefundsAsync(payment.Id, ct);
        decimal succeeded = refunds.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount);
        decimal pending = refunds.Where(r => r.Status == RefundStatus.Pending).Sum(r => r.Amount);
        return new RefundSummary(
            payment.Currency, payment.Amount, succeeded, pending, payment.Amount - succeeded - pending,
            refunds.Select(r => new RefundInfo(r.Id, r.Amount, r.Status.ToString(), r.CreatedAt)).ToList());
    }
}
