using System.Globalization;
using System.Text;
using Ecommerce.Orders.Contracts;

namespace Ecommerce.Notifications.Application;

/// <summary>Plain-text emails. Order money is the order's own snapshot, formatted with the invariant culture.</summary>
internal static class EmailTemplates
{
    public static (string Subject, string Body) OrderConfirmation(string name, OrderNotificationView order)
    {
        StringBuilder body = new();
        body.AppendLine($"Hi {name},");
        body.AppendLine();
        body.AppendLine($"Thank you. We received your payment for order {order.OrderNumber}.");
        body.AppendLine();
        foreach (OrderNotificationLine line in order.Lines)
        {
            body.AppendLine($"{line.Quantity} x {line.Name}  {Money(line.UnitPrice, order.Currency)}");
        }
        body.AppendLine();
        body.AppendLine($"Total: {Money(order.GrandTotal, order.Currency)}");
        return ($"Order {order.OrderNumber} confirmed", body.ToString());
    }

    public static (string Subject, string Body) OrderShipped(string name, OrderNotificationView order)
    {
        StringBuilder body = new();
        body.AppendLine($"Hi {name},");
        body.AppendLine();
        body.AppendLine($"Order {order.OrderNumber} is on its way.");
        if (order.LatestShipment is { } shipment)
        {
            body.AppendLine();
            body.AppendLine($"Carrier: {shipment.CarrierName}");
            body.AppendLine($"Tracking number: {shipment.TrackingNumber}");
            if (shipment.TrackingUrl is not null)
            {
                body.AppendLine($"Track it: {shipment.TrackingUrl}");
            }
        }
        return ($"Order {order.OrderNumber} has shipped", body.ToString());
    }

    private static string Money(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} {currency}");
}
