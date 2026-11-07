namespace Ecommerce.Orders.Domain;

/// <summary>The eight order states (arch 7.3). Stored and sent as their names.</summary>
internal enum OrderStatus
{
    PendingPayment,
    Paid,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Refunded,
    Returned,
}
