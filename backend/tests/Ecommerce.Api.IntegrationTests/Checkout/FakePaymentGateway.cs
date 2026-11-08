using Ecommerce.Payments.Application;
using Ecommerce.Payments.Contracts;
using Ecommerce.Persistence;

namespace Ecommerce.Api.IntegrationTests.Checkout;

internal sealed record FakeIntent(string Id, string Secret, IntentRequest Request);

/// <summary>What the fake gateway saw. One instance per test host (singleton); the gateway itself is scoped.</summary>
internal sealed class FakeGatewayState
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, FakeIntent> _byKey = [];
    private readonly Dictionary<string, FakeIntent> _byId = [];
    private readonly List<IntentRequest> _creates = [];
    private readonly List<RefundCall> _refundCalls = [];
    private readonly Dictionary<string, string> _refundsByKey = [];
    private readonly List<string> _cancels = [];
    private int _retrieves;
    private int _transactionViolations;

    /// <summary>When true every call throws PaymentProviderUnavailableException, like an unreachable provider.</summary>
    public bool Unavailable { get; set; }

    /// <summary>Every CreateIntentAsync call, repeats included.</summary>
    public IReadOnlyList<IntentRequest> Creates
    {
        get
        {
            lock (_gate)
            {
                return _creates.ToList();
            }
        }
    }

    /// <summary>Distinct intents (the provider returns the same intent for the same idempotency key).</summary>
    public IReadOnlyList<FakeIntent> Intents
    {
        get
        {
            lock (_gate)
            {
                return _byKey.Values.ToList();
            }
        }
    }

    public int Retrieves => Volatile.Read(ref _retrieves);

    /// <summary>Every CreateRefundAsync call, repeats included.</summary>
    public IReadOnlyList<RefundCall> RefundCalls
    {
        get
        {
            lock (_gate)
            {
                return _refundCalls.ToList();
            }
        }
    }

    /// <summary>Distinct provider refunds: like Stripe, the same idempotency key returns the same refund.</summary>
    public int DistinctRefunds
    {
        get
        {
            lock (_gate)
            {
                return _refundsByKey.Count;
            }
        }
    }

    public IReadOnlyList<string> Cancels
    {
        get
        {
            lock (_gate)
            {
                return _cancels.ToList();
            }
        }
    }

    public string RefundIdOf(string idempotencyKey)
    {
        lock (_gate)
        {
            return _refundsByKey[idempotencyKey];
        }
    }

    public string CreateRefund(RefundCall call)
    {
        lock (_gate)
        {
            _refundCalls.Add(call);
            if (Unavailable)
            {
                throw new PaymentProviderUnavailableException("fake provider unavailable");
            }
            if (!_refundsByKey.TryGetValue(call.IdempotencyKey, out string? id))
            {
                id = "re_fake_" + Guid.NewGuid().ToString("N");
                _refundsByKey[call.IdempotencyKey] = id;
            }
            return id;
        }
    }

    public void Cancel(string providerPaymentId)
    {
        lock (_gate)
        {
            if (Unavailable)
            {
                throw new PaymentProviderUnavailableException("fake provider unavailable");
            }
            _cancels.Add(providerPaymentId);
        }
    }

    /// <summary>Calls made while a database transaction was open on the request scope (rule 6). Must stay 0.</summary>
    public int TransactionViolations => Volatile.Read(ref _transactionViolations);

    public FakeIntent Create(IntentRequest request)
    {
        lock (_gate)
        {
            _creates.Add(request);
            if (Unavailable)
            {
                throw new PaymentProviderUnavailableException("fake provider unavailable");
            }
            if (_byKey.TryGetValue(request.IdempotencyKey, out FakeIntent? existing))
            {
                return existing;
            }
            FakeIntent created = new("pi_fake_" + Guid.NewGuid().ToString("N"), "secret_fake_" + Guid.NewGuid().ToString("N"), request);
            _byKey[request.IdempotencyKey] = created;
            _byId[created.Id] = created;
            return created;
        }
    }

    public string Retrieve(string providerPaymentId)
    {
        lock (_gate)
        {
            Interlocked.Increment(ref _retrieves);
            if (Unavailable)
            {
                throw new PaymentProviderUnavailableException("fake provider unavailable");
            }
            return _byId[providerPaymentId].Secret;
        }
    }

    public void NoteTransactionViolation() => Interlocked.Increment(ref _transactionViolations);
}

/// <summary>
/// A gateway that behaves like the provider (same key, same intent) and fails loudly if a database transaction is open on
/// the request scope when it is called: no external call inside a transaction (rule 6).
/// </summary>
internal sealed class FakePaymentGateway(FakeGatewayState state, EcommerceDbContext db) : IPaymentGateway
{
    public Task<CreatedIntent> CreateIntentAsync(IntentRequest request, CancellationToken ct)
    {
        RequireNoTransaction();
        FakeIntent intent = state.Create(request);
        return Task.FromResult(new CreatedIntent(intent.Id, intent.Secret));
    }

    public Task<string> GetClientSecretAsync(string providerPaymentId, CancellationToken ct)
    {
        RequireNoTransaction();
        return Task.FromResult(state.Retrieve(providerPaymentId));
    }

    public Task<string> CreateRefundAsync(RefundCall call, CancellationToken ct)
    {
        RequireNoTransaction();
        return Task.FromResult(state.CreateRefund(call));
    }

    public Task CancelIntentAsync(string providerPaymentId, CancellationToken ct)
    {
        RequireNoTransaction();
        state.Cancel(providerPaymentId);
        return Task.CompletedTask;
    }

    private void RequireNoTransaction()
    {
        if (db.Database.CurrentTransaction is not null)
        {
            state.NoteTransactionViolation();
            throw new InvalidOperationException("The payment gateway was called inside a database transaction (rule 6).");
        }
    }
}
