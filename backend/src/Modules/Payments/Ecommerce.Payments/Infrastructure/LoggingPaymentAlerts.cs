using Ecommerce.Payments.Application;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Ecommerce.Payments.Infrastructure;

/// <summary>Structured logs, ids only: no amounts of card data, no secrets. The future alert hook (Slice 7).</summary>
internal sealed class LoggingPaymentAlerts(ILogger<LoggingPaymentAlerts> logger) : IPaymentAlerts
{
    public static readonly EventId PaymentAlert = new(4001, nameof(PaymentAlert));

    public void AutoRefundIssued(Guid paymentId, Guid orderId, Guid refundId, string reason) =>
        logger.LogCritical(
            PaymentAlert,
            "Payment {PaymentId} for order {OrderId} succeeded but no order can use it ({Reason}); automatic refund {RefundId} requested",
            paymentId, orderId, reason, refundId);

    public void RefundFailed(Guid refundId, Guid paymentId) =>
        logger.LogCritical(PaymentAlert, "Refund {RefundId} on payment {PaymentId} failed at the provider; its amount is free under the ceiling again", refundId, paymentId);

    public void RefundOrderNotTransitioned(Guid refundId, Guid orderId) =>
        logger.LogCritical(PaymentAlert, "Refund {RefundId} reached the captured amount but order {OrderId} could not move; check the order", refundId, orderId);

    public void IntentCreationFailed(Guid orderId, Guid paymentId, int attempt, Exception? providerError)
    {
        // Only the provider's error type and code: the exception message could echo request data, so it is not logged.
        StripeError? error = (providerError as StripeException)?.StripeError;
        logger.LogWarning(
            "Creating the payment intent failed. OrderId={OrderId} PaymentId={PaymentId} Attempt={Attempt} StripeErrorType={StripeErrorType} StripeErrorCode={StripeErrorCode}",
            orderId, paymentId, attempt, error?.Type, error?.Code);
    }

    public void UnknownIntent(string eventId, string providerPaymentId) =>
        logger.LogWarning("Webhook event {EventId} names unknown payment intent {ProviderPaymentId}; ignored", eventId, providerPaymentId);
}
