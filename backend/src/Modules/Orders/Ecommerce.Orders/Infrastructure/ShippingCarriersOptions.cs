using System.Text.RegularExpressions;
using Ecommerce.Orders.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Orders.Infrastructure;

internal sealed class CarrierOption
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string TrackingUrlTemplate { get; set; } = "";
}

internal sealed class ShippingCarriersOptions
{
    public List<CarrierOption> Carriers { get; set; } = [];
}

/// <summary>Validated on start (arch 7.4). An empty list is valid: ship then answers 400 until the owner supplies carriers.</summary>
internal sealed partial class ShippingCarriersOptionsValidator(ILogger<ShippingCarriersOptionsValidator> logger)
    : IValidateOptions<ShippingCarriersOptions>
{
    [GeneratedRegex(@"^[a-z0-9-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public ValidateOptionsResult Validate(string? name, ShippingCarriersOptions options)
    {
        if (options.Carriers.Count == 0)
        {
            logger.LogWarning("Shipping:Carriers is empty; the admin ship action will answer 400 until carriers are configured.");
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        HashSet<string> seen = [];
        for (int i = 0; i < options.Carriers.Count; i++)
        {
            CarrierOption c = options.Carriers[i];
            if (!CodePattern().IsMatch(c.Code))
            {
                failures.Add($"Shipping:Carriers:{i}:Code must match ^[a-z0-9-]+$.");
            }
            else if (!seen.Add(c.Code))
            {
                failures.Add($"Shipping:Carriers:{i}:Code '{c.Code}' is duplicated.");
            }
            if (c.Name.Length is < 1 or > 100)
            {
                failures.Add($"Shipping:Carriers:{i}:Name must be 1 to 100 characters.");
            }
            if (!Uri.TryCreate(c.TrackingUrlTemplate, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
                || !c.TrackingUrlTemplate.Contains(ShippingCarriers.Placeholder, StringComparison.Ordinal))
            {
                failures.Add($"Shipping:Carriers:{i}:TrackingUrlTemplate must be an absolute https URL containing {ShippingCarriers.Placeholder}.");
            }
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
