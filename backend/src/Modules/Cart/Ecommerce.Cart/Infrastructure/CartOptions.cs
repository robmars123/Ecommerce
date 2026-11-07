using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Cart.Infrastructure;

internal sealed class CartOptions
{
    public const int MaxAllowedQuantity = 100;

    public int MaxQuantityPerLine { get; set; } = 10;
    public string Currency { get; set; } = "USD";
}

internal sealed partial class CartOptionsValidator : IValidateOptions<CartOptions>
{
    public ValidateOptionsResult Validate(string? name, CartOptions options)
    {
        List<string> failures = [];
        if (options.MaxQuantityPerLine is < 1 or > CartOptions.MaxAllowedQuantity)
        {
            failures.Add($"Cart:MaxQuantityPerLine must be 1 to {CartOptions.MaxAllowedQuantity}.");
        }
        if (!CurrencyPattern().IsMatch(options.Currency))
        {
            failures.Add("Store:Currency must be a 3-letter upper-case code such as USD.");
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();
}
