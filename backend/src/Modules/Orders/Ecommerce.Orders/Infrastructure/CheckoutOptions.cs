using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Orders.Infrastructure;

internal sealed class ShippingMethodOption
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int EstimatedDays { get; set; }
}

/// <summary>
/// The checkout configuration (arch 7.4): <c>Shipping:Methods</c>, <c>Shipping:Countries</c>, <c>Tax:RatePercent</c>,
/// <c>Store:Currency</c>, <c>Checkout:ReservationTtlMinutes</c>. Values come from the owner; the code invents none (Slice 4 D14).
/// </summary>
internal sealed class CheckoutOptions
{
    public List<ShippingMethodOption> Methods { get; set; } = [];
    public List<string> Countries { get; set; } = [];
    public decimal TaxRatePercent { get; set; }
    public string Currency { get; set; } = "USD";
    public int ReservationTtlMinutes { get; set; } = 15;
}

/// <summary>Validated on start. Empty method or country lists are valid but warn: checkout answers 400 until the owner supplies them.</summary>
internal sealed partial class CheckoutOptionsValidator(ILogger<CheckoutOptionsValidator> logger) : IValidateOptions<CheckoutOptions>
{
    [GeneratedRegex(@"^[a-z0-9-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^[A-Z]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CountryPattern();

    [GeneratedRegex(@"^[A-Z]{3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();

    public ValidateOptionsResult Validate(string? name, CheckoutOptions options)
    {
        List<string> failures = [];
        if (!CurrencyPattern().IsMatch(options.Currency))
        {
            failures.Add("Store:Currency must be a three-letter ISO 4217 code in capitals.");
        }
        if (options.ReservationTtlMinutes is < 1 or > 120)
        {
            failures.Add("Checkout:ReservationTtlMinutes must be 1 to 120.");
        }
        if (options.TaxRatePercent is < 0m or > 100m)
        {
            failures.Add("Tax:RatePercent must be 0 to 100.");
        }
        for (int i = 0; i < options.Countries.Count; i++)
        {
            if (!CountryPattern().IsMatch(options.Countries[i]))
            {
                failures.Add($"Shipping:Countries:{i} must be a two-letter ISO 3166-1 code in capitals.");
            }
        }

        HashSet<string> seen = [];
        for (int i = 0; i < options.Methods.Count; i++)
        {
            ShippingMethodOption m = options.Methods[i];
            if (!CodePattern().IsMatch(m.Code))
            {
                failures.Add($"Shipping:Methods:{i}:Code must match ^[a-z0-9-]+$.");
            }
            else if (!seen.Add(m.Code))
            {
                failures.Add($"Shipping:Methods:{i}:Code '{m.Code}' is duplicated.");
            }
            if (m.Name.Length is < 1 or > 100)
            {
                failures.Add($"Shipping:Methods:{i}:Name must be 1 to 100 characters.");
            }
            if (m.Amount < 0m || m.Amount != decimal.Round(m.Amount, 2))
            {
                failures.Add($"Shipping:Methods:{i}:Amount must be 0 or more with at most two decimal places.");
            }
            if (m.EstimatedDays < 0)
            {
                failures.Add($"Shipping:Methods:{i}:EstimatedDays must be 0 or more.");
            }
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }
        if (options.Methods.Count == 0 || options.Countries.Count == 0)
        {
            logger.LogWarning("Shipping:Methods or Shipping:Countries is empty; checkout answers 400 until both are configured.");
        }
        return ValidateOptionsResult.Success;
    }
}
