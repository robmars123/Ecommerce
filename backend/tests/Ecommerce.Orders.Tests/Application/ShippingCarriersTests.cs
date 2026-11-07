using Ecommerce.Orders.Application;
using Ecommerce.Orders.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Orders.Tests.Application;

public class ShippingCarriersTests
{
    private static ShippingCarriers Carriers() =>
        new ShippingCarriers([new Carrier("example-post", "Example Post", "https://tracking.example.com/t/{trackingNumber}")]);

    [Fact]
    public void The_tracking_number_is_url_encoded_into_the_template()
    {
        Assert.Equal("https://tracking.example.com/t/AB%2012%2F3%3Fx%3D1", Carriers().BuildTrackingUrl("example-post", "AB 12/3?x=1"));
        Assert.Equal("Example Post", Carriers().NameOf("example-post"));
    }

    [Fact]
    public void A_code_that_is_no_longer_configured_keeps_its_code_as_the_name_and_has_no_url()
    {
        Assert.Null(Carriers().BuildTrackingUrl("gone", "T-1"));
        Assert.Equal("gone", Carriers().NameOf("gone"));
        Assert.False(Carriers().Exists("gone"));
        Assert.True(Carriers().Exists("example-post"));
    }

    private static ValidateOptionsResult Validate(params CarrierOption[] carriers) =>
        new ShippingCarriersOptionsValidator(NullLogger<ShippingCarriersOptionsValidator>.Instance)
            .Validate(null, new ShippingCarriersOptions { Carriers = carriers.ToList() });

    private static CarrierOption Good(string code = "example-post") =>
        new CarrierOption { Code = code, Name = "Example Post", TrackingUrlTemplate = "https://tracking.example.com/{trackingNumber}" };

    [Fact]
    public void An_empty_list_and_a_good_list_are_valid() 
    {
        Assert.True(Validate().Succeeded);
        Assert.True(Validate(Good("a"), Good("b")).Succeeded);
    }

    [Fact]
    public void A_duplicate_code_is_rejected() => Assert.True(Validate(Good("a"), Good("a")).Failed);

    [Theory]
    [InlineData("Upper")]
    [InlineData("has space")]
    [InlineData("")]
    public void A_bad_code_is_rejected(string code) => Assert.True(Validate(Good(code)).Failed);

    [Theory]
    [InlineData("http://tracking.example.com/{trackingNumber}")]
    [InlineData("https://tracking.example.com/track")]
    [InlineData("/relative/{trackingNumber}")]
    [InlineData("")]
    public void A_template_must_be_absolute_https_and_contain_the_placeholder(string template)
    {
        CarrierOption carrier = Good();
        carrier.TrackingUrlTemplate = template;
        Assert.True(Validate(carrier).Failed);
    }

    [Fact]
    public void A_name_of_zero_or_101_characters_is_rejected()
    {
        CarrierOption empty = Good();
        empty.Name = "";
        CarrierOption longName = Good();
        longName.Name = new string('n', 101);
        Assert.True(Validate(empty).Failed);
        Assert.True(Validate(longName).Failed);
    }
}
