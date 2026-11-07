namespace Ecommerce.Payments.Domain;

internal enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    PartiallyRefunded,
    Refunded,
}
