using System.Security.Cryptography;
using System.Text;
using Ecommerce.Orders.Contracts;
using Ecommerce.Payments.Application;
using Ecommerce.Payments.Contracts;
using Ecommerce.Payments.Domain;
using Ecommerce.Payments.Infrastructure;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payments.Tests.Application;

internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
}

internal sealed class FakeKeys : IIdempotencyStore
{
    public List<IdempotencyEntry> Entries { get; } = [];

    public Task<IdempotencyEntry?> FindAsync(string scope, string key, CancellationToken ct) =>
        Task.FromResult(Entries.FirstOrDefault(e => e.Scope == scope && e.Key == key));

    public void Add(IdempotencyEntry entry) => Entries.Add(entry);
}

internal sealed class FakeOutbox : IOutbox
{
    public List<OutboxMessage> Messages { get; } = [];

    public void Add(OutboxMessage message) => Messages.Add(message);
}

internal sealed class FakeAudit : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];

    public void Add(AuditEntry entry) => Entries.Add(entry);
}

internal sealed class FakeOrderRefunds : IOrderRefunds
{
    public RefundOrderInfo? Info { get; set; } = new("Paid", Guid.NewGuid(), false);
    public int Locks { get; private set; }

    public Task<RefundOrderInfo?> LockForRefundAsync(Guid orderId, CancellationToken ct)
    {
        Locks++;
        return Task.FromResult(Info);
    }

    public Task<bool> ApplyFullRefundAsync(Guid orderId, Guid refundId, Guid? actorId, CancellationToken ct) => Task.FromResult(true);

    public Task MarkReturnRefundedAsync(Guid orderId, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class FakePaymentStore : IPaymentStore
{
    public Payment? Paying { get; set; }
    public List<Refund> Refunds { get; } = [];

    public void Add(Payment payment) => Paying = payment;

    public void AddRefund(Refund refund) => Refunds.Add(refund);

    public Task<Payment?> LockPayingPaymentAsync(Guid orderId, CancellationToken ct) => Task.FromResult(Paying);

    public Task<Payment?> LockByIdAsync(Guid paymentId, CancellationToken ct) => Task.FromResult(Paying);

    public Task<Payment?> GetPayingAsync(Guid orderId, CancellationToken ct) => Task.FromResult(Paying);

    public Task<decimal> SumRefundsAsync(Guid paymentId, IReadOnlyCollection<RefundStatus> statuses, IReadOnlyCollection<Guid> except, CancellationToken ct) =>
        Task.FromResult(Refunds.Where(r => statuses.Contains(r.Status) && !except.Contains(r.Id)).Sum(r => r.Amount));

    public Task<IReadOnlyList<Refund>> ListRefundsAsync(Guid paymentId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Refund>>(Refunds.ToList());

    public Task<Refund?> FindRefundAsync(Guid? refundId, string? providerRefundId, CancellationToken ct) =>
        Task.FromResult(Refunds.FirstOrDefault(r => r.Id == refundId || (providerRefundId != null && r.ProviderRefundId == providerRefundId)));

    public Task<Refund?> GetRefundTrackedAsync(Guid refundId, CancellationToken ct) => Task.FromResult(Refunds.FirstOrDefault(r => r.Id == refundId));

    public Task SetProviderRefundIdAsync(Guid refundId, string providerRefundId, CancellationToken ct) => Task.CompletedTask;

    public Task<Payment?> GetAsync(Guid paymentId, CancellationToken ct) => Task.FromResult(Paying);

    public Task<Payment?> GetLatestAsync(Guid orderId, CancellationToken ct) => Task.FromResult(Paying);

    public Task<IReadOnlyList<Payment>> ListOpenIntentsAsync(Guid orderId, Guid exceptPaymentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Payment>>([]);

    public Task<Payment?> LockByProviderIdAsync(string providerPaymentId, CancellationToken ct) => Task.FromResult(Paying);

    public Task<string?> AttachProviderIdAsync(Guid paymentId, string providerPaymentId, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult<string?>(providerPaymentId);
}

// Refund domain, the admin refund decisions (arch 8.5 steps 1 to 3), the read side, and the refund webhook mapping.
public class RefundTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Admin = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Payment PaymentOf(decimal amount = 25m, PaymentStatus status = PaymentStatus.Succeeded)
    {
        Payment p = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 1, amount, "USD", Now);
        if (status != PaymentStatus.Pending)
        {
            p.Succeed(Now);
            typeof(Payment).GetProperty(nameof(Payment.Status))!.SetValue(p, status);
        }
        return p;
    }

    private sealed record Rig(
        RefundService Service, FakePaymentStore Store, FakeKeys Keys, FakeOutbox Outbox, FakeAudit Audit, FakeOrderRefunds Orders, Payment Payment);

    private static Rig NewRig(string orderStatus = "Paid", bool approvedReturn = false)
    {
        FakePaymentStore store = new() { Paying = PaymentOf() };
        FakeKeys keys = new();
        FakeOutbox outbox = new();
        FakeAudit audit = new();
        FakeOrderRefunds orders = new() { Info = new RefundOrderInfo(orderStatus, Guid.NewGuid(), approvedReturn) };
        return new Rig(new RefundService(new FakeUnitOfWork(), store, keys, orders, outbox, audit, new FakeClock(Now)), store, keys, outbox, audit, orders, store.Paying);
    }

    private static RefundCommand Command(Rig rig, decimal amount, string key = "k-1", string currency = "USD", string reason = "Damaged") =>
        new(Admin, rig.Payment.OrderId, amount, currency, reason, key);

    // ---- domain ----

    [Fact]
    public void An_admin_refund_is_Pending_with_the_actor_and_key_and_a_terminal_event_moves_it_once()
    {
        Payment payment = PaymentOf();
        Refund refund = Refund.CreateAdmin(Guid.NewGuid(), payment, 10m, "why", Admin, "key-1", Now);

        Assert.Equal((RefundStatus.Pending, Admin, "key-1", 10m, "USD"), (refund.Status, refund.ActorId, refund.IdempotencyKey, refund.Amount, refund.Currency));
        Assert.True(refund.Succeed("re_1", Now));
        Assert.False(refund.Succeed("re_2", Now));
        Assert.False(refund.Fail(Now));
        Assert.Equal((RefundStatus.Succeeded, "re_1", Now), (refund.Status, refund.ProviderRefundId, refund.CompletedAt));
    }

    [Fact]
    public void A_failed_refund_cannot_later_succeed_and_a_known_provider_id_is_kept()
    {
        Refund refund = Refund.CreateAdmin(Guid.NewGuid(), PaymentOf(), 10m, "why", Admin, "key-1", Now);

        Assert.True(refund.Fail(Now));
        Assert.False(refund.Succeed("re_1", Now));
        Assert.Equal(RefundStatus.Failed, refund.Status);
        Assert.Null(refund.ProviderRefundId);
    }

    [Theory]
    [InlineData(0, 25, true)]
    [InlineData(0, 25.01, false)]
    [InlineData(20, 5, true)]
    [InlineData(20, 5.01, false)]
    [InlineData(25, 0.01, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, -1, false)]
    public void The_ceiling_allows_pending_plus_succeeded_plus_this_up_to_the_captured_amount_and_never_zero_or_negative(double committed, double amount, bool allowed) =>
        Assert.Equal(allowed, PaymentOf().CanCover((decimal)committed, (decimal)amount));

    [Theory]
    [InlineData(0, "Succeeded")]
    [InlineData(10, "PartiallyRefunded")]
    [InlineData(25, "Refunded")]
    public void The_payment_status_follows_the_succeeded_total(double succeeded, string expected)
    {
        Payment payment = PaymentOf();

        payment.ApplyRefundedTotal((decimal)succeeded, Now.AddMinutes(1));

        Assert.Equal(Enum.Parse<PaymentStatus>(expected), payment.Status);
    }

    [Fact]
    public void A_payment_that_never_succeeded_is_not_refundable_or_changed_by_a_refund_total()
    {
        Payment pending = PaymentOf(status: PaymentStatus.Pending);

        pending.ApplyRefundedTotal(25m, Now);

        Assert.False(pending.IsRefundable);
        Assert.Equal(PaymentStatus.Pending, pending.Status);
        Assert.True(PaymentOf(status: PaymentStatus.PartiallyRefunded).IsRefundable);
        Assert.False(PaymentOf(status: PaymentStatus.Refunded).IsRefundable);
    }

    // ---- RefundService ----

    [Fact]
    public async Task A_partial_refund_inserts_the_Pending_refund_audit_outbox_and_key_and_answers_with_server_totals()
    {
        Rig rig = NewRig();

        RefundResult result = await rig.Service.RequestAsync(Command(rig, 10m), Ct);

        RefundAccepted accepted = Assert.IsType<RefundResult.Accepted>(result).Value;
        Assert.Equal(("Pending", 10m, 0m, 15m, "USD"), (accepted.Status, accepted.Amount, accepted.RefundedTotal, accepted.Remaining, accepted.Currency));
        Refund refund = Assert.Single(rig.Store.Refunds);
        Assert.Equal((Admin, "k-1"), (refund.ActorId, refund.IdempotencyKey));
        OutboxMessage staged = Assert.Single(rig.Outbox.Messages);
        Assert.Equal(("RefundRequested", refund.Id), (staged.EventType, staged.AggregateId));
        AuditEntry audit = Assert.Single(rig.Audit.Entries);
        Assert.Equal(("refund.request", Admin, "Damaged"), (audit.Action, audit.ActorId, audit.Reason));
        Assert.NotNull(audit.BeforeJson);
        Assert.NotNull(audit.AfterJson);
        Assert.Single(rig.Keys.Entries);
    }

    [Fact]
    public async Task Over_the_ceiling_is_a_conflict_with_an_audit_row_and_no_refund_and_no_key()
    {
        Rig rig = NewRig();
        await rig.Service.RequestAsync(Command(rig, 20m, "k-1"), Ct);

        RefundResult result = await rig.Service.RequestAsync(Command(rig, 5.01m, "k-2"), Ct);

        Assert.Equal("refund-ceiling-exceeded", Assert.IsType<RefundResult.Conflict>(result).Type);
        Assert.Single(rig.Store.Refunds);
        Assert.Single(rig.Keys.Entries);
        Assert.Equal(["refund.request", "refund.rejected"], rig.Audit.Entries.Select(a => a.Action));
    }

    [Fact]
    public async Task A_failed_refund_frees_its_amount_under_the_ceiling()
    {
        Rig rig = NewRig();
        await rig.Service.RequestAsync(Command(rig, 25m, "k-1"), Ct);
        Assert.IsType<RefundResult.Conflict>(await rig.Service.RequestAsync(Command(rig, 5m, "k-2"), Ct));

        rig.Store.Refunds[0].Fail(Now);

        Assert.IsType<RefundResult.Accepted>(await rig.Service.RequestAsync(Command(rig, 5m, "k-3"), Ct));
    }

    [Fact]
    public async Task The_same_key_and_body_replays_the_stored_response_and_changes_nothing()
    {
        Rig rig = NewRig();
        RefundAccepted first = Assert.IsType<RefundResult.Accepted>(await rig.Service.RequestAsync(Command(rig, 10m), Ct)).Value;

        RefundAccepted again = Assert.IsType<RefundResult.Accepted>(await rig.Service.RequestAsync(Command(rig, 10m), Ct)).Value;

        Assert.Equal(first, again);
        Assert.Single(rig.Store.Refunds);
        Assert.Single(rig.Audit.Entries);
        Assert.Single(rig.Outbox.Messages);
    }

    [Theory]
    [InlineData(11, "USD", "Damaged")]
    [InlineData(10, "USD", "Another reason")]
    [InlineData(10, "EUR", "Damaged")]
    public async Task The_same_key_with_a_different_body_is_a_key_conflict(double amount, string currency, string reason)
    {
        Rig rig = NewRig();
        await rig.Service.RequestAsync(Command(rig, 10m), Ct);

        RefundResult result = await rig.Service.RequestAsync(Command(rig, (decimal)amount, currency: currency, reason: reason), Ct);

        Assert.IsType<RefundResult.KeyConflict>(result);
        Assert.Single(rig.Store.Refunds);
    }

    [Fact]
    public async Task A_currency_other_than_the_payments_or_a_third_decimal_is_invalid()
    {
        Rig rig = NewRig();

        Assert.Equal("amount.currency", Assert.IsType<RefundResult.Invalid>(await rig.Service.RequestAsync(Command(rig, 5m, currency: "EUR"), Ct)).Field);
        Assert.Equal("amount.amount", Assert.IsType<RefundResult.Invalid>(await rig.Service.RequestAsync(Command(rig, 5.001m, "k-2"), Ct)).Field);
        Assert.Empty(rig.Store.Refunds);
    }

    [Theory]
    [InlineData("Paid", false, 10, true)]
    [InlineData("Processing", false, 10, true)]
    [InlineData("Shipped", false, 10, true)]
    [InlineData("Delivered", false, 10, true)]
    [InlineData("PendingPayment", false, 10, false)]
    [InlineData("Cancelled", false, 10, false)]
    [InlineData("Refunded", false, 10, false)]
    [InlineData("Returned", false, 10, false)]
    [InlineData("Paid", false, 25, true)]
    [InlineData("Processing", false, 25, true)]
    [InlineData("Shipped", false, 25, false)]
    [InlineData("Delivered", false, 25, false)]
    [InlineData("Delivered", true, 25, true)]
    [InlineData("Delivered", true, 10, true)]
    public async Task The_order_state_decides_whether_a_partial_or_a_full_refund_is_allowed(string status, bool approvedReturn, double amount, bool allowed)
    {
        Rig rig = NewRig(status, approvedReturn);

        RefundResult result = await rig.Service.RequestAsync(Command(rig, (decimal)amount), Ct);

        Assert.Equal(allowed, result is RefundResult.Accepted);
        if (!allowed)
        {
            Assert.Equal("refund-state-not-allowed", Assert.IsType<RefundResult.Conflict>(result).Type);
            Assert.Empty(rig.Store.Refunds);
        }
    }

    [Fact]
    public async Task A_top_up_that_reaches_the_captured_amount_counts_as_full_for_the_state_rules()
    {
        Rig rig = NewRig("Shipped");
        Assert.IsType<RefundResult.Accepted>(await rig.Service.RequestAsync(Command(rig, 20m, "k-1"), Ct));

        Assert.IsType<RefundResult.Conflict>(await rig.Service.RequestAsync(Command(rig, 5m, "k-2"), Ct));
    }

    [Fact]
    public async Task No_paying_payment_is_NotFound_for_an_unknown_order_and_a_conflict_for_an_unpaid_one()
    {
        Rig rig = NewRig();
        rig.Store.Paying = null;
        rig.Orders.Info = null;
        Assert.IsType<RefundResult.NotFound>(await rig.Service.RequestAsync(Command(rig, 5m), Ct));

        rig.Orders.Info = new RefundOrderInfo("PendingPayment", Guid.NewGuid(), false);
        Assert.Equal("no-refundable-payment", Assert.IsType<RefundResult.Conflict>(await rig.Service.RequestAsync(Command(rig, 5m, "k-2"), Ct)).Type);
    }

    // ---- RefundState ----

    [Fact]
    public async Task A_pending_full_refund_is_one_where_pending_plus_succeeded_cover_the_captured_amount()
    {
        FakePaymentStore store = new() { Paying = PaymentOf() };
        RefundState state = new(store);
        Guid order = store.Paying!.OrderId;
        Assert.False(await state.HasPendingFullRefundAsync(order, Ct));

        Refund first = Refund.CreateAdmin(Guid.NewGuid(), store.Paying, 10m, "r", Admin, "k1", Now);
        store.AddRefund(first);
        Assert.False(await state.HasPendingFullRefundAsync(order, Ct));

        store.AddRefund(Refund.CreateAdmin(Guid.NewGuid(), store.Paying, 15m, "r", Admin, "k2", Now));
        Assert.True(await state.HasPendingFullRefundAsync(order, Ct));

        store.Refunds[1].Fail(Now);
        Assert.False(await state.HasPendingFullRefundAsync(order, Ct));
        first.Succeed("re_1", Now);
        Assert.False(await state.HasPendingFullRefundAsync(order, Ct)); // nothing pending any more
    }

    [Fact]
    public async Task The_summary_has_server_computed_totals_and_every_refund_in_order()
    {
        FakePaymentStore store = new() { Paying = PaymentOf() };
        Refund done = Refund.CreateAdmin(Guid.NewGuid(), store.Paying!, 10m, "r", Admin, "k1", Now);
        done.Succeed("re_1", Now);
        store.AddRefund(done);
        store.AddRefund(Refund.CreateAdmin(Guid.NewGuid(), store.Paying!, 5m, "r", Admin, "k2", Now.AddMinutes(1)));
        store.AddRefund(Refund.CreateAdmin(Guid.NewGuid(), store.Paying!, 3m, "r", Admin, "k3", Now.AddMinutes(2)));
        store.Refunds[2].Fail(Now);

        RefundSummary summary = await new RefundState(store).GetSummaryAsync(store.Paying!.OrderId, Ct);

        Assert.Equal((25m, 10m, 5m, 10m, "USD"), (summary.Captured, summary.RefundedTotal, summary.PendingTotal, summary.Remaining, summary.Currency));
        Assert.Equal(["Succeeded", "Pending", "Failed"], summary.Refunds.Select(r => r.Status));
    }

    [Fact]
    public async Task With_no_paying_payment_the_summary_is_empty_and_zero()
    {
        RefundSummary summary = await new RefundState(new FakePaymentStore()).GetSummaryAsync(Guid.NewGuid(), Ct);

        Assert.Equal((0m, 0m, null), (summary.Remaining, summary.Captured, summary.Currency));
        Assert.Empty(summary.Refunds);
    }

    // ---- webhook mapping ----

    private static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private static PaymentEvent Verify(string body)
    {
        long t = Now.ToUnixTimeSeconds();
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(Secret));
        string header = $"t={t},v1={Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{t}.{body}")))}";
        Assert.True(new StripeWebhookVerifier(Options.Create(new StripeOptions { WebhookSecret = Secret })).TryVerify(body, header, Now, out PaymentEvent? evt));
        return evt!;
    }

    [Theory]
    [InlineData("succeeded", "Succeeded")]
    [InlineData("failed", "Failed")]
    [InlineData("canceled", "Failed")]
    [InlineData("pending", "Pending")]
    [InlineData("requires_action", "Pending")]
    public void A_refund_updated_event_reports_our_id_the_provider_id_and_the_mapped_status(string status, string expectedName)
    {
        Guid ours = Guid.NewGuid();
        string body = $"{{\"id\":\"evt_r\",\"type\":\"refund.updated\",\"data\":{{\"object\":{{\"id\":\"re_1\",\"status\":\"{status}\",\"metadata\":{{\"refund_id\":\"{ours}\"}}}}}}}}";

        PaymentEvent evt = Verify(body);

        Assert.Equal(PaymentEventKind.Refund, evt.Kind);
        Assert.Null(evt.ProviderPaymentId);
        RefundNoticeStatus expected = Enum.Parse<RefundNoticeStatus>(expectedName);
        Assert.Equal((ours, "re_1", expected), (evt.Refunds![0].RefundId, evt.Refunds[0].ProviderRefundId, evt.Refunds[0].Status));
    }

    [Fact]
    public void A_refund_without_our_metadata_id_is_found_by_the_provider_id_and_a_bad_metadata_id_is_ignored()
    {
        string body = "{\"id\":\"evt_r\",\"type\":\"refund.updated\",\"data\":{\"object\":{\"id\":\"re_9\",\"status\":\"succeeded\",\"metadata\":{\"refund_id\":\"nope\"}}}}";

        RefundNotice notice = Verify(body).Refunds![0];

        Assert.Equal((null, "re_9"), (notice.RefundId, notice.ProviderRefundId));
    }

    [Fact]
    public void A_charge_refunded_event_reports_each_expanded_refund_and_none_when_not_expanded()
    {
        Guid ours = Guid.NewGuid();
        string expanded = $"{{\"id\":\"evt_c\",\"type\":\"charge.refunded\",\"data\":{{\"object\":{{\"id\":\"ch_1\",\"refunds\":{{\"data\":[{{\"id\":\"re_1\",\"status\":\"succeeded\",\"metadata\":{{\"refund_id\":\"{ours}\"}}}},{{\"id\":\"re_2\",\"status\":\"failed\"}}]}}}}}}}}";
        string bare = "{\"id\":\"evt_c2\",\"type\":\"charge.refunded\",\"data\":{\"object\":{\"id\":\"ch_1\"}}}";

        PaymentEvent withRefunds = Verify(expanded);
        PaymentEvent without = Verify(bare);

        Assert.Equal([RefundNoticeStatus.Succeeded, RefundNoticeStatus.Failed], withRefunds.Refunds!.Select(r => r.Status));
        Assert.Equal(PaymentEventKind.Refund, without.Kind);
        Assert.Empty(without.Refunds!);
    }

    [Fact]
    public async Task The_refund_consumer_calls_the_provider_with_the_re_key_and_stores_the_provider_id_and_a_missing_payment_id_throws()
    {
        FakeGateway gateway = new();
        FakePaymentStore store = new();
        RefundRequestedConsumer consumer = new(gateway, store);
        Guid refundId = Guid.NewGuid();
        Payment payment = PaymentOf();
        Refund refund = Refund.CreateAdmin(refundId, payment, 12.34m, "r", Admin, "k", Now);
        payment = PaymentWithIntent(payment, "pi_1");

        await consumer.HandleAsync(PaymentOutbox.RefundRequest(refund, payment, Now), Ct);

        RefundCall call = Assert.Single(gateway.Refunds);
        Assert.Equal(($"re:{refundId}", 1234L, "pi_1", "USD", refundId), (call.IdempotencyKey, call.AmountMinor, call.ProviderPaymentId, call.Currency, call.RefundId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.HandleAsync(PaymentOutbox.RefundRequest(refund, PaymentOf(), Now), Ct));
    }

    [Fact]
    public async Task The_cancel_consumer_cancels_the_intent_named_in_the_event()
    {
        FakeGateway gateway = new();
        Payment payment = PaymentWithIntent(PaymentOf(status: PaymentStatus.Pending), "pi_77");

        await new IntentCancelConsumer(gateway).HandleAsync(PaymentOutbox.CancelIntent(payment, Now), Ct);

        Assert.Equal(["pi_77"], gateway.Cancels);
    }

    private static Payment PaymentWithIntent(Payment payment, string intent)
    {
        typeof(Payment).GetProperty(nameof(Payment.ProviderPaymentId))!.SetValue(payment, intent);
        return payment;
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public List<RefundCall> Refunds { get; } = [];
        public List<string> Cancels { get; } = [];

        public Task<CreatedIntent> CreateIntentAsync(IntentRequest request, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> GetClientSecretAsync(string providerPaymentId, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> CreateRefundAsync(RefundCall call, CancellationToken ct)
        {
            Refunds.Add(call);
            return Task.FromResult("re_fake");
        }

        public Task CancelIntentAsync(string providerPaymentId, CancellationToken ct)
        {
            Cancels.Add(providerPaymentId);
            return Task.CompletedTask;
        }
    }
}
