namespace Ecommerce.Orders.Application;

internal sealed record Carrier(string Code, string Name, string TrackingUrlTemplate);

/// <summary>The configured carriers (arch 7.4). The tracking URL is built here, on the server, never stored or client-built.</summary>
internal sealed class ShippingCarriers(IReadOnlyList<Carrier> carriers)
{
    public const string Placeholder = "{trackingNumber}";

    public bool Exists(string code) => carriers.Any(c => c.Code == code);

    /// <summary>A code that is no longer configured still shows as itself rather than hiding the shipment.</summary>
    public string NameOf(string code) => carriers.FirstOrDefault(c => c.Code == code)?.Name ?? code;

    public string? BuildTrackingUrl(string code, string trackingNumber)
    {
        Carrier? carrier = carriers.FirstOrDefault(c => c.Code == code);
        return carrier?.TrackingUrlTemplate.Replace(Placeholder, Uri.EscapeDataString(trackingNumber), StringComparison.Ordinal);
    }
}
