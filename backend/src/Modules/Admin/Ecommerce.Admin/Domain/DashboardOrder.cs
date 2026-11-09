namespace Ecommerce.Admin.Domain;

/// <summary>
/// One row per order in the dashboard read model (Slice 6b D9). Built only from outbox events. GrandTotal, Currency and PaidAt stay
/// null for an order that never reached Paid (for example an expired one).
/// </summary>
internal sealed class DashboardOrder
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "";
    public decimal? GrandTotal { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}

/// <summary>One row per succeeded refund that an actor asked for (admin or saga). Automatic refunds are not revenue and never appear.</summary>
internal sealed class DashboardRefund
{
    public Guid RefundId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset SucceededAt { get; set; }
}
