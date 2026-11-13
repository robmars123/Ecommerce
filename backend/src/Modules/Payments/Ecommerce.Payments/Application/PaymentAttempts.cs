using Ecommerce.Payments.Contracts;
using Ecommerce.Payments.Domain;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Payments.Application;

/// <summary>
/// The public side of Payments for checkout and retry (arch 8.2 rules 1, 4, 11). Start and cancel only stage rows in the
/// caller transaction; <see cref="EnsureIntentAsync"/> is the one provider call and runs after commit (rule 6).
/// </summary>
internal sealed class PaymentAttempts(IPaymentStore store, IPaymentGateway gateway, IOutbox outbox, IClock clock, IPaymentAlerts alerts) : IPaymentAttempts
{
    public async Task<PaymentAttemptInfo> StartAttemptAsync(Guid paymentId, Guid orderId, decimal amount, string currency, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        Payment? previous = await store.GetLatestAsync(orderId, ct);
        if (previous is { HasCancellableIntent: true })
        {
            outbox.Add(PaymentOutbox.CancelIntent(previous, now));
        }

        Payment payment = Payment.Create(paymentId, orderId, (previous?.Attempt ?? 0) + 1, amount, currency, now);
        store.Add(payment);
        return ToInfo(payment);
    }

    public async Task RequestCancelOpenIntentAsync(Guid orderId, CancellationToken ct)
    {
        Payment? latest = await store.GetLatestAsync(orderId, ct);
        if (latest is { HasCancellableIntent: true })
        {
            outbox.Add(PaymentOutbox.CancelIntent(latest, clock.UtcNow));
        }
    }

    public async Task<PaymentAttemptInfo?> GetLatestAsync(Guid orderId, CancellationToken ct)
    {
        Payment? latest = await store.GetLatestAsync(orderId, ct);
        return latest is null ? null : ToInfo(latest);
    }

    public async Task<PaymentIntentHandle> EnsureIntentAsync(Guid paymentId, CancellationToken ct)
    {
        Payment payment = await store.GetAsync(paymentId, ct) ?? throw new InvalidOperationException($"Payment {paymentId} does not exist.");
        if (payment.ProviderPaymentId is not null)
        {
            // Never re-create: the provider idempotency key lives 24 hours, the order can outlive that.
            string secret = await gateway.GetClientSecretAsync(payment.ProviderPaymentId, ct);
            return new PaymentIntentHandle(payment.ProviderPaymentId, secret, payment.Attempt);
        }

        CreatedIntent created;
        try
        {
            created = await gateway.CreateIntentAsync(
                new IntentRequest($"pi:{payment.OrderId}:{payment.Attempt}", MinorUnits.From(payment.Amount), payment.Currency, payment.OrderId, payment.Id), ct);
        }
        catch (PaymentProviderUnavailableException ex)
        {
            // The caller answers 503 "retry"; this is the only place that records why (ids and the provider error type/code, no secrets).
            alerts.IntentCreationFailed(payment.OrderId, payment.Id, payment.Attempt, ex.InnerException);
            throw;
        }
        // A concurrent repeat stores the same id (same provider key); the first write wins.
        string stored = await store.AttachProviderIdAsync(payment.Id, created.ProviderPaymentId, clock.UtcNow, ct) ?? created.ProviderPaymentId;
        return new PaymentIntentHandle(stored, created.ClientSecret, payment.Attempt);
    }

    private static PaymentAttemptInfo ToInfo(Payment p) => new(p.Id, p.Attempt, p.Status.ToString(), p.ProviderPaymentId, PaymentMethods.Card);
}
