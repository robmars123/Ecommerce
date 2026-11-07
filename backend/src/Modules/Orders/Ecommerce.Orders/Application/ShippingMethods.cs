namespace Ecommerce.Orders.Application;

internal sealed record ShippingMethod(string Code, string Name, decimal Amount, int EstimatedDays);

/// <summary>Checkout values from configuration (arch 7.4), built once by the module. Never from the client.</summary>
internal sealed record CheckoutSettings(string Currency, decimal TaxRatePercent, int ReservationTtlMinutes);

/// <summary>The configured flat-rate methods and ship-to countries (arch 7.4). A concrete class: there is one rate rule in v1 (Slice 4 D14).</summary>
internal sealed class ShippingMethods(IReadOnlyList<ShippingMethod> methods, IReadOnlyList<string> countries)
{
    public IReadOnlyList<ShippingMethod> All => methods;

    public ShippingMethod? Find(string? code) => methods.FirstOrDefault(m => m.Code == code);

    public bool ShipsTo(string country) => countries.Contains(country, StringComparer.Ordinal);
}
