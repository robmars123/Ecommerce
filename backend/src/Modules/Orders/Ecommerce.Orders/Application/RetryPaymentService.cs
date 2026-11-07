using Ecommerce.Inventory.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.Payments.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Orders.Application;

/// <summary>
/// Retry payment (arch 8.2 rule 11, Slice 4 D12). Owner only, while the order is PendingPayment and its reservation has not
/// expired by the clock (the sweep need not have run). One short transaction adds attempt n+1 at the stored grand total and
/// leaves the reservation alone; the provider intent is created after commit. A repeated key returns the same attempt.
/// </summary>
internal sealed class RetryPaymentService(
    IOrderStore orders,
    IInventoryReserver reserver,
    IPaymentAttempts payments,
    IIdempotencyStore keys,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const string Scope = "retry-payment";
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(24);

    private sealed class Rejected(RetryResult result) : Exception("Retry rejected.")
    {
        public RetryResult Result { get; } = result;
    }

    private abstract record Started
    {
        private Started() { }

        public sealed record Attempt(Guid PaymentId) : Started;

        public sealed record Conflict : Started;
    }

    public async Task<RetryResult> RetryAsync(Guid userId, Guid orderId, string idempotencyKey, CancellationToken ct)
    {
        Guid? owner = await orders.GetOwnerAsync(orderId, ct);
        if (owner is null)
        {
            return new RetryResult.NotFound();
        }
        if (owner != userId)
        {
            return new RetryResult.Forbidden();
        }

        string hash = orderId.ToString("D");
        Started started;
        try
        {
            started = await unitOfWork.InTransactionAsync(token => StartAsync(userId, orderId, idempotencyKey, hash, token), ct);
        }
        catch (Rejected rejected)
        {
            return rejected.Result;
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == CheckoutService.KeyConstraint)
        {
            IdempotencyEntry? seen = await keys.FindAsync(Scope, idempotencyKey, ct);
            started = Matches(seen, userId, hash, out Guid existing) ? new Started.Attempt(existing) : new Started.Conflict();
        }

        if (started is not Started.Attempt attempt)
        {
            return new RetryResult.KeyConflict();
        }
        try
        {
            PaymentIntentHandle handle = await payments.EnsureIntentAsync(attempt.PaymentId, ct);
            return new RetryResult.Ok(orderId, handle.Attempt, handle.ClientSecret);
        }
        catch (PaymentProviderUnavailableException)
        {
            return new RetryResult.ProviderUnavailable(orderId);
        }
    }

    private async Task<Started> StartAsync(Guid userId, Guid orderId, string key, string hash, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        IdempotencyEntry? seen = await keys.FindAsync(Scope, key, ct);
        if (seen is not null)
        {
            return Matches(seen, userId, hash, out Guid existing) ? new Started.Attempt(existing) : new Started.Conflict();
        }

        Guid paymentId = Guid.CreateVersion7(now);
        keys.Add(new IdempotencyEntry(Scope, key, userId, hash, paymentId.ToString(), null, now, now + KeyLifetime));
        await unitOfWork.SaveChangesAsync(ct);

        Order order = await orders.LockForUpdateAsync(orderId, ct) ?? throw new Rejected(new RetryResult.NotFound());
        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new Rejected(new RetryResult.NotPending());
        }
        DateTimeOffset? expiresAt = await reserver.GetActiveExpiryAsync(orderId, ct);
        if (expiresAt is null || expiresAt <= now)
        {
            throw new Rejected(new RetryResult.ReservationExpired());
        }

        await payments.StartAttemptAsync(paymentId, orderId, order.GrandTotal, order.Currency, ct);
        return new Started.Attempt(paymentId);
    }

    private static bool Matches(IdempotencyEntry? seen, Guid userId, string hash, out Guid paymentId)
    {
        paymentId = Guid.Empty;
        return seen is not null && seen.UserId == userId && seen.RequestHash == hash && Guid.TryParse(seen.ResultRef, out paymentId);
    }
}
