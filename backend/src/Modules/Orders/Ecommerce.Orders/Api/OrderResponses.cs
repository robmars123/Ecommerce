using System.Globalization;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;

namespace Ecommerce.Orders.Api;

// Wire shapes (arch 9). Money is a decimal string. No user id and no actor id appears anywhere.
internal sealed record MoneyResponse(string Amount, string Currency)
{
    public static MoneyResponse From(decimal amount, string currency) =>
        new MoneyResponse(amount.ToString("0.00##", CultureInfo.InvariantCulture), currency);
}

internal sealed record OrderListItemResponse(
    Guid Id, string OrderNumber, DateTimeOffset CreatedAt, string Status, MoneyResponse GrandTotal, string? TrackingUrl);

internal sealed record OrderListResponse(IReadOnlyList<OrderListItemResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

internal sealed record OrderItemResponse(
    Guid VariantId, string Sku, string Name, MoneyResponse UnitPrice, int Quantity, MoneyResponse LineTotal);

internal sealed record ShippingMethodResponse(string Code, string Name);

internal sealed record TimelineEntryResponse(string? FromStatus, string ToStatus, DateTimeOffset At, string? Reason);

internal sealed record ShipmentResponse(
    string CarrierName, string TrackingNumber, string? TrackingUrl, DateTimeOffset ShippedAt, DateTimeOffset? DeliveredAt);

internal sealed record OrderDetailResponse(
    Guid Id,
    string OrderNumber,
    DateTimeOffset CreatedAt,
    string Status,
    IReadOnlyList<OrderItemResponse> Items,
    MoneyResponse Subtotal,
    MoneyResponse DiscountTotal,
    MoneyResponse ShippingTotal,
    MoneyResponse TaxTotal,
    MoneyResponse GrandTotal,
    ShippingMethodResponse ShippingMethod,
    ShippingAddress ShippingAddress,
    IReadOnlyList<TimelineEntryResponse> Timeline,
    IReadOnlyList<ShipmentResponse> Shipments,
    string? PaymentStatus);

internal static class OrderResponseMapping
{
    public static OrderListResponse ToResponse(this OrderListPage page) =>
        new OrderListResponse(
            page.Items.Select(i => new OrderListItemResponse(
                i.Id, i.OrderNumber, i.CreatedAt, i.Status.ToString(), MoneyResponse.From(i.GrandTotal, i.Currency), i.TrackingUrl)).ToList(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);

    public static OrderDetailResponse ToResponse(this OrderDetailView view)
    {
        OrderDetailRow o = view.Order;
        return new OrderDetailResponse(
            o.Id, o.OrderNumber, o.CreatedAt, o.Status.ToString(),
            o.Items.Select(i => new OrderItemResponse(
                i.VariantId, i.Sku, i.Name, MoneyResponse.From(i.UnitPrice, o.Currency), i.Quantity, MoneyResponse.From(i.LineTotal, o.Currency))).ToList(),
            MoneyResponse.From(o.Subtotal, o.Currency),
            MoneyResponse.From(o.DiscountTotal, o.Currency),
            MoneyResponse.From(o.ShippingTotal, o.Currency),
            MoneyResponse.From(o.TaxTotal, o.Currency),
            MoneyResponse.From(o.GrandTotal, o.Currency),
            new ShippingMethodResponse(o.ShippingMethodCode, o.ShippingMethodName),
            o.ShippingAddress,
            o.Timeline.Select(t => new TimelineEntryResponse(t.FromStatus?.ToString(), t.ToStatus.ToString(), t.At, t.Reason)).ToList(),
            view.Shipments.Select(s => new ShipmentResponse(s.CarrierName, s.TrackingNumber, s.TrackingUrl, s.ShippedAt, s.DeliveredAt)).ToList(),
            view.PaymentStatus);
    }
}

// Checkout wire shapes (arch 9 checkout contract). The order block is the same snapshot the order detail shows.
internal sealed record CheckoutOrderResponse(
    Guid Id,
    string OrderNumber,
    string Status,
    IReadOnlyList<OrderItemResponse> Items,
    MoneyResponse Subtotal,
    MoneyResponse DiscountTotal,
    MoneyResponse ShippingTotal,
    MoneyResponse TaxTotal,
    MoneyResponse GrandTotal,
    ShippingMethodResponse ShippingMethod,
    ShippingAddress ShippingAddress);

internal sealed record CheckoutResponse(CheckoutOrderResponse Order, string? ClientSecret);

internal sealed record ShippingMethodItemResponse(string Code, string Name, MoneyResponse Price, int EstimatedDays);

internal sealed record ShippingMethodsResponse(IReadOnlyList<ShippingMethodItemResponse> Items);

internal sealed record RetryPaymentResponse(Guid OrderId, int Attempt, string ClientSecret);

internal static class CheckoutResponseMapping
{
    public static CheckoutResponse ToResponse(this CheckoutResult.Ok ok)
    {
        OrderDetailRow o = ok.Order;
        return new CheckoutResponse(
            new CheckoutOrderResponse(
                o.Id, o.OrderNumber, o.Status.ToString(),
                o.Items.Select(i => new OrderItemResponse(
                    i.VariantId, i.Sku, i.Name, MoneyResponse.From(i.UnitPrice, o.Currency), i.Quantity, MoneyResponse.From(i.LineTotal, o.Currency))).ToList(),
                MoneyResponse.From(o.Subtotal, o.Currency),
                MoneyResponse.From(o.DiscountTotal, o.Currency),
                MoneyResponse.From(o.ShippingTotal, o.Currency),
                MoneyResponse.From(o.TaxTotal, o.Currency),
                MoneyResponse.From(o.GrandTotal, o.Currency),
                new ShippingMethodResponse(o.ShippingMethodCode, o.ShippingMethodName),
                o.ShippingAddress),
            ok.ClientSecret);
    }
}
