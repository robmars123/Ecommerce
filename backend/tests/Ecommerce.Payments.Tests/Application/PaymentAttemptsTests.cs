using Ecommerce.Payments.Application;
using Ecommerce.Payments.Contracts;
using Ecommerce.Payments.Domain;
using Ecommerce.Payments.Infrastructure;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Ecommerce.Payments.Tests.Application;

public class PaymentAttemptsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record LogEntry(LogLevel Level, string Text, IReadOnlyDictionary<string, string> State, bool HasException);

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, string> fields = [];
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (KeyValuePair<string, object?> pair in pairs)
                {
                    fields[pair.Key] = pair.Value?.ToString() ?? "";
                }
            }
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), fields, exception is not null));
        }
    }

    private sealed class ThrowingGateway(Exception toThrow) : IPaymentGateway
    {
        public Task<CreatedIntent> CreateIntentAsync(IntentRequest request, CancellationToken ct) => throw toThrow;

        public Task<string> GetClientSecretAsync(string providerPaymentId, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> CreateRefundAsync(RefundCall call, CancellationToken ct) => throw new NotSupportedException();

        public Task CancelIntentAsync(string providerPaymentId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RecordingAlerts : IPaymentAlerts
    {
        public List<(Guid OrderId, Guid PaymentId, int Attempt, Exception? Error)> IntentFailures { get; } = [];

        public void IntentCreationFailed(Guid orderId, Guid paymentId, int attempt, Exception? providerError) => IntentFailures.Add((orderId, paymentId, attempt, providerError));

        public void AutoRefundIssued(Guid paymentId, Guid orderId, Guid refundId, string reason) => throw new NotSupportedException();

        public void UnknownIntent(string eventId, string providerPaymentId) => throw new NotSupportedException();

        public void RefundFailed(Guid refundId, Guid paymentId) => throw new NotSupportedException();

        public void RefundOrderNotTransitioned(Guid refundId, Guid orderId) => throw new NotSupportedException();
    }

    [Fact]
    public async Task A_failed_intent_creation_reports_the_ids_and_the_provider_error_once_and_rethrows()
    {
        Payment payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 2, 123.45m, "USD", Now);
        StripeException inner = new("boom");
        RecordingAlerts alerts = new();
        PaymentAttempts attempts = new(new FakePaymentStore { Paying = payment }, new ThrowingGateway(new PaymentProviderUnavailableException("down", inner)), new FakeOutbox(), new FakeClock(Now), alerts);

        await Assert.ThrowsAsync<PaymentProviderUnavailableException>(() => attempts.EnsureIntentAsync(payment.Id, Ct));

        Assert.Equal((payment.OrderId, payment.Id, 2, (Exception?)inner), Assert.Single(alerts.IntentFailures));
    }

    [Fact]
    public void The_logged_intent_failure_has_the_ids_and_the_stripe_type_and_code_and_no_secret_or_amount()
    {
        Guid orderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        Guid paymentId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        StripeException stripe = new("sk_live_SHOULD_NOT_APPEAR amount 123.45") { StripeError = new StripeError { Type = "invalid_request_error", Code = "amount_too_small" } };
        CollectingLogger<LoggingPaymentAlerts> logger = new();

        new LoggingPaymentAlerts(logger).IntentCreationFailed(orderId, paymentId, 2, stripe);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(orderId.ToString(), entry.State["OrderId"]);
        Assert.Equal(paymentId.ToString(), entry.State["PaymentId"]);
        Assert.Equal("2", entry.State["Attempt"]);
        Assert.Equal("invalid_request_error", entry.State["StripeErrorType"]);
        Assert.Equal("amount_too_small", entry.State["StripeErrorCode"]);
        string everything = entry.Text + string.Join(' ', entry.State.Values);
        Assert.DoesNotContain("secret", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk_", everything);
        Assert.DoesNotContain("123.45", everything);
        Assert.DoesNotContain("12345", everything);
        Assert.False(entry.HasException);
    }

    [Fact]
    public void A_non_stripe_cause_logs_the_ids_with_empty_error_fields()
    {
        CollectingLogger<LoggingPaymentAlerts> logger = new();

        new LoggingPaymentAlerts(logger).IntentCreationFailed(Guid.NewGuid(), Guid.NewGuid(), 1, new HttpRequestException("timeout"));

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal("", entry.State["StripeErrorCode"]);
    }
}
