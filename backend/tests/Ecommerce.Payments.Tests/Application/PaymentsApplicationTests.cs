using System.Security.Cryptography;
using System.Text;
using Ecommerce.Payments.Application;
using Ecommerce.Payments.Domain;
using Ecommerce.Payments.Infrastructure;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payments.Tests.Application;

public class PaymentsApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private static StripeWebhookVerifier Verifier(string? secret = null) =>
        new(Options.Create(new StripeOptions { WebhookSecret = secret ?? Secret }));

    private static string Sign(string body, DateTimeOffset at, string? secret = null)
    {
        long t = at.ToUnixTimeSeconds();
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret ?? Secret));
        return $"t={t},v1={Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{t}.{body}")))}";
    }

    private static string Body(string type, string intent = "pi_1") =>
        $"{{\"id\":\"evt_1\",\"type\":\"{type}\",\"api_version\":\"2099-01-01\",\"data\":{{\"object\":{{\"id\":\"{intent}\",\"amount_received\":2500,\"currency\":\"usd\",\"unknown_shape\":[1,2]}}}}}}";

    [Theory]
    [InlineData("payment_intent.succeeded", "Succeeded")]
    [InlineData("payment_intent.payment_failed", "Failed")]
    [InlineData("payment_intent.canceled", "Canceled")]
    public void A_signed_event_yields_its_id_type_and_intent_whatever_the_api_version(string type, string kindName)
    {
        string body = Body(type);

        bool ok = Verifier().TryVerify(body, Sign(body, Now), Now, out PaymentEvent? evt);

        Assert.True(ok);
        Assert.Equal(("evt_1", type, Enum.Parse<PaymentEventKind>(kindName), "pi_1"), (evt!.EventId, evt.EventType, evt.Kind, evt.ProviderPaymentId));
    }

    [Fact]
    public void An_event_type_we_ignore_is_verified_but_has_no_intent()
    {
        string body = Body("charge.succeeded");

        bool ok = Verifier().TryVerify(body, Sign(body, Now), Now, out PaymentEvent? evt);

        Assert.True(ok);
        Assert.Equal((PaymentEventKind.Other, null), (evt!.Kind, evt.ProviderPaymentId));
    }

    [Fact]
    public void A_wrong_secret_a_stale_timestamp_a_tampered_body_a_missing_header_or_no_configured_secret_is_rejected()
    {
        string body = Body("payment_intent.succeeded");

        Assert.False(Verifier().TryVerify(body, Sign(body, Now, secret: "other"), Now, out PaymentEvent? a));
        Assert.False(Verifier().TryVerify(body, Sign(body, Now.AddMinutes(-6)), Now, out PaymentEvent? b));
        Assert.False(Verifier().TryVerify(body + " ", Sign(body, Now), Now, out PaymentEvent? c));
        Assert.False(Verifier().TryVerify(body, null, Now, out PaymentEvent? d));
        Assert.False(Verifier(secret: "").TryVerify(body, Sign(body, Now), Now, out PaymentEvent? e));
        Assert.All(new[] { a, b, c, d, e }, evt => Assert.Null(evt));
    }

    [Fact]
    public void A_signed_event_with_no_intent_id_is_rejected()
    {
        string body = "{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{}}}";

        Assert.False(Verifier().TryVerify(body, Sign(body, Now), Now, out PaymentEvent? evt));
        Assert.Null(evt);
    }

    [Theory]
    [InlineData("{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_1\",\"currency\":\"usd\"}}}")]
    [InlineData("{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_1\",\"amount_received\":2500}}}")]
    [InlineData("{\"id\":\"evt_1\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_1\",\"amount_received\":\"2500\",\"currency\":\"usd\"}}}")]
    public void A_success_without_a_numeric_amount_received_or_a_currency_is_rejected(string body)
    {
        Assert.False(Verifier().TryVerify(body, Sign(body, Now), Now, out PaymentEvent? evt));
        Assert.Null(evt);
    }

    [Fact]
    public void A_success_carries_the_amount_received_and_currency_a_failure_does_not_need_them()
    {
        string success = Body("payment_intent.succeeded");
        string failure = "{\"id\":\"evt_2\",\"type\":\"payment_intent.payment_failed\",\"data\":{\"object\":{\"id\":\"pi_1\"}}}";

        Assert.True(Verifier().TryVerify(success, Sign(success, Now), Now, out PaymentEvent? ok));
        Assert.Equal((2500L, "usd"), (ok!.AmountReceived, ok.Currency));
        Assert.True(Verifier().TryVerify(failure, Sign(failure, Now), Now, out PaymentEvent? failed));
        Assert.Equal(PaymentEventKind.Failed, failed!.Kind);
    }

    [Fact]
    public void An_automatic_refund_can_be_for_the_amount_and_currency_actually_received()
    {
        Payment payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 2, 25.00m, "USD", Now);

        Refund refund = Refund.CreateAutomatic(Guid.NewGuid(), payment, Refund.AmountMismatch, Now, 24.99m, "EUR");

        Assert.Equal((24.99m, "EUR", "amount-mismatch", $"auto:{payment.Id}"), (refund.Amount, refund.Currency, refund.Reason, refund.IdempotencyKey));
    }

    [Fact]
    public void An_automatic_refund_is_the_whole_payment_pending_with_no_actor_and_the_auto_key()
    {
        Payment payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 2, 25.00m, "USD", Now);

        Refund refund = Refund.CreateAutomatic(Guid.NewGuid(), payment, Refund.LateSuccess, Now);

        Assert.Equal((payment.Id, 25.00m, "USD", RefundStatus.Pending, (Guid?)null), (refund.PaymentId, refund.Amount, refund.Currency, refund.Status, refund.ActorId));
        Assert.Equal($"auto:{payment.Id}", refund.IdempotencyKey);
        Assert.Equal("late-success", refund.Reason);
    }

    [Theory]
    [InlineData("25.00", 2500)]
    [InlineData("0.01", 1)]
    [InlineData("1606", 160600)]
    public void Minor_units_are_the_amount_times_one_hundred(string amount, long expected) =>
        Assert.Equal(expected, MinorUnits.From(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void An_amount_that_is_not_a_whole_number_of_cents_fails_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => MinorUnits.From(10.005m));
        Assert.Throws<InvalidOperationException>(() => MinorUnits.From(-1m));
    }

    [Fact]
    public void Only_a_pending_or_failed_attempt_with_an_intent_has_a_cancellable_intent()
    {
        Payment none = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 5m, "USD", Now);
        Assert.False(none.HasCancellableIntent);

        Payment pending = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 5m, "USD", Now);
        typeof(Payment).GetProperty(nameof(Payment.ProviderPaymentId))!.SetValue(pending, "pi_1");
        Assert.True(pending.HasCancellableIntent);

        pending.Succeed(Now);
        Assert.False(pending.HasCancellableIntent);
    }
}
