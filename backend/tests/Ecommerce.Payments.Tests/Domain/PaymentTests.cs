using Ecommerce.Payments.Domain;

namespace Ecommerce.Payments.Tests.Domain;

/// <summary>Arch 7.3 payment machine: Pending to Succeeded or Failed; a verified success after a decline still counts (Slice 4 D8).</summary>
public class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Payment Pending() => Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 1, 25.00m, "USD", Now);

    private static Payment In(PaymentStatus status)
    {
        Payment p = Pending();
        typeof(Payment).GetProperty(nameof(Payment.Status))!.SetValue(p, status);
        return p;
    }

    [Fact]
    public void A_new_payment_is_Pending_for_the_given_attempt_and_amount()
    {
        Payment p = Pending();

        Assert.Equal((PaymentStatus.Pending, 1, 25.00m, "USD", "stripe", null), (p.Status, p.Attempt, p.Amount, p.Currency, p.Provider, p.ProviderPaymentId));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Failed")]
    public void Succeed_moves_Pending_and_Failed_to_Succeeded(string from)
    {
        Payment p = In(Enum.Parse<PaymentStatus>(from));

        bool changed = p.Succeed(Now.AddMinutes(1));

        Assert.True(changed);
        Assert.Equal(PaymentStatus.Succeeded, p.Status);
        Assert.Equal(Now.AddMinutes(1), p.UpdatedAt);
    }

    [Theory]
    [InlineData("Succeeded")]
    [InlineData("PartiallyRefunded")]
    [InlineData("Refunded")]
    public void Succeed_on_an_already_succeeded_payment_is_a_no_op(string from)
    {
        PaymentStatus status = Enum.Parse<PaymentStatus>(from);
        Payment p = In(status);

        Assert.False(p.Succeed(Now.AddMinutes(1)));

        Assert.Equal(status, p.Status);
    }

    [Fact]
    public void Fail_moves_Pending_to_Failed()
    {
        Payment p = Pending();

        Assert.True(p.Fail(Now.AddMinutes(1)));

        Assert.Equal(PaymentStatus.Failed, p.Status);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Succeeded")]
    [InlineData("PartiallyRefunded")]
    [InlineData("Refunded")]
    public void Fail_never_downgrades_a_payment_that_is_not_Pending(string from)
    {
        PaymentStatus status = Enum.Parse<PaymentStatus>(from);
        Payment p = In(status);

        Assert.False(p.Fail(Now.AddMinutes(1)));

        Assert.Equal(status, p.Status);
    }
}
