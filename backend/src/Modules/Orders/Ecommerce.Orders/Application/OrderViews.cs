using Ecommerce.Catalog.Contracts;
using Ecommerce.Orders.Domain;
using Ecommerce.Payments.Contracts;

namespace Ecommerce.Orders.Application;

// Read-store rows: raw database values, no carrier lookups, no actor ids (changed_by is never selected).
internal sealed record OrderListRow(
    Guid Id,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    decimal GrandTotal,
    string Currency,
    string? CarrierCode,
    string? TrackingNumber);

internal sealed record OrderListRows(IReadOnlyList<OrderListRow> Items, int TotalCount);

internal sealed record OrderItemRow(Guid VariantId, string Sku, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

internal sealed record TimelineRow(OrderStatus? FromStatus, OrderStatus ToStatus, DateTimeOffset At, string? Reason);

internal sealed record ShipmentRow(string Carrier, string TrackingNumber, DateTimeOffset ShippedAt, DateTimeOffset? DeliveredAt);

internal sealed record OrderDetailRow(
    Guid Id,
    Guid UserId,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    string Currency,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal ShippingTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    string ShippingMethodCode,
    string ShippingMethodName,
    ShippingAddress ShippingAddress,
    IReadOnlyList<OrderItemRow> Items,
    IReadOnlyList<TimelineRow> Timeline,
    IReadOnlyList<ShipmentRow> Shipments);

// Service views: carrier name and tracking URL resolved from configuration.
internal sealed record OrderListItem(
    Guid Id,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    OrderStatus Status,
    decimal GrandTotal,
    string Currency,
    string? TrackingUrl);

internal sealed record OrderListPage(IReadOnlyList<OrderListItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

internal sealed record AdminOrderListItem(
    Guid Id, string OrderNumber, string? CustomerEmail, DateTimeOffset CreatedAt, string Status, decimal GrandTotal, string Currency);

internal sealed record AdminOrderListPage(IReadOnlyList<AdminOrderListItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);

internal sealed record ShipmentView(string CarrierName, string TrackingNumber, string? TrackingUrl, DateTimeOffset ShippedAt, DateTimeOffset? DeliveredAt);

/// <summary>PaymentStatus is the latest attempt (arch 9 orders contract), or null before any attempt.</summary>
internal sealed record OrderDetailView(
    OrderDetailRow Order, IReadOnlyList<ShipmentView> Shipments, string? PaymentStatus, IReadOnlyList<RefundInfo> Refunds,
    IReadOnlyDictionary<Guid, VariantSnapshot> Variants);

/// <summary>The customer detail plus what F13 needs (D14): every refund, the server-computed totals and the return requests.</summary>
internal sealed record AdminOrderDetailView(OrderDetailView Detail, RefundSummary Summary, IReadOnlyList<ReturnRow> Returns);

/// <summary>Ok | NotFound | Forbidden (the order belongs to someone else, Test 7).</summary>
internal abstract record OrderReadResult<T>
{
    private OrderReadResult() { }

    public sealed record Ok(T Value) : OrderReadResult<T>;

    public sealed record NotFound : OrderReadResult<T>;

    public sealed record Forbidden : OrderReadResult<T>;
}

internal enum FulfillmentOutcome
{
    Ok,
    NotFound,
    /// <summary>The request itself is wrong (unknown carrier): 400.</summary>
    Invalid,
    /// <summary>The order is not in a state that allows this, or another admin got there first: 409.</summary>
    Conflict,
}

internal sealed record FulfillmentResult(FulfillmentOutcome Outcome, string? Field = null, string? Message = null);
