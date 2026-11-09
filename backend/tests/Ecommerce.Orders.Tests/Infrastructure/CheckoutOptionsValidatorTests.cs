using Ecommerce.Orders.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Orders.Tests.Infrastructure;

public class CheckoutOptionsValidatorTests
{
    private static CheckoutOptions Valid() => new()
    {
        Methods = [new ShippingMethodOption { Code = "standard", Name = "Standard", Amount = 5.00m, EstimatedDays = 5 }],
        Countries = ["US"],
        TaxRatePercent = 10m,
        Currency = "USD",
        ReservationTtlMinutes = 15,
    };

    private static ValidateOptionsResult Run(CheckoutOptions options) =>
        new CheckoutOptionsValidator(NullLogger<CheckoutOptionsValidator>.Instance).Validate(null, options);

    [Fact]
    public void A_valid_configuration_passes()
    {
        Assert.True(Run(Valid()).Succeeded);
    }

    [Fact]
    public void Empty_method_and_country_lists_pass()
    {
        CheckoutOptions options = Valid();
        options.Methods = [];
        options.Countries = [];

        Assert.True(Run(options).Succeeded);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("4.999")]
    public void An_amount_that_is_negative_or_has_more_than_two_decimals_fails(string amount)
    {
        CheckoutOptions options = Valid();
        options.Methods[0].Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(Run(options).Failed);
    }

    [Fact]
    public void A_duplicate_method_code_fails()
    {
        CheckoutOptions options = Valid();
        options.Methods.Add(new ShippingMethodOption { Code = "standard", Name = "Again", Amount = 1m, EstimatedDays = 1 });

        Assert.True(Run(options).Failed);
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("a b")]
    [InlineData("")]
    public void A_method_code_outside_the_pattern_fails(string code)
    {
        CheckoutOptions options = Valid();
        options.Methods[0].Code = code;

        Assert.True(Run(options).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void A_method_name_outside_1_to_100_characters_fails(int length)
    {
        CheckoutOptions options = Valid();
        options.Methods[0].Name = new string('n', length);

        Assert.True(Run(options).Failed);
    }

    [Fact]
    public void Negative_estimated_days_fail()
    {
        CheckoutOptions options = Valid();
        options.Methods[0].EstimatedDays = -1;

        Assert.True(Run(options).Failed);
    }

    [Fact]
    public void A_lowercase_country_fails()
    {
        CheckoutOptions options = Valid();
        options.Countries = ["us"];

        Assert.True(Run(options).Failed);
    }

    [Fact]
    public void A_lowercase_currency_fails()
    {
        CheckoutOptions options = Valid();
        options.Currency = "usd";

        Assert.True(Run(options).Failed);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("100.01")]
    public void A_tax_rate_outside_0_to_100_fails(string rate)
    {
        CheckoutOptions options = Valid();
        options.TaxRatePercent = decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(Run(options).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void A_reservation_ttl_outside_1_to_120_fails(int minutes)
    {
        CheckoutOptions options = Valid();
        options.ReservationTtlMinutes = minutes;

        Assert.True(Run(options).Failed);
    }
}
