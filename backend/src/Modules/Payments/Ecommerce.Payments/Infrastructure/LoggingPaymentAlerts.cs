using Ecommerce.Payments.Application;
using Microsoft.Extensions.Logging;

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

    public void UnknownIntent(string eventId, string providerPaymentId) =>
        logger.LogWarning("Webhook event {EventId} names unknown payment intent {ProviderPaymentId}; ignored", eventId, providerPaymentId);
}
