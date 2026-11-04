using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class ProductVariantRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddHours(1);

    private static Product NewProduct() =>
        Product.Create(Guid.NewGuid(), "blue-shirt", "Blue Shirt", "A shirt", Guid.NewGuid(), T0);

    private static ProductVariant Add(Product p, string sku = "BS-M", decimal price = 10m, string currency = "USD",
        decimal? compareAt = null, int weight = 100) =>
        p.AddVariant(Guid.NewGuid(), sku, "Medium", "M", "Blue", price, currency, compareAt, weight, T1);

    [Fact]
    public void AddVariant_accepts_valid_variant_with_null_compare_at()
    {
        var p = NewProduct();
        var v = Add(p);
        Assert.Single(p.Variants);
        Assert.True(v.IsActive);
        Assert.Equal(p.Id, v.ProductId);
        Assert.Equal(T1, p.UpdatedAt);
    }

    [Fact]
    public void AddVariant_accepts_zero_price()
    {
        var p = NewProduct();
        Add(p, price: 0m);
        Assert.Single(p.Variants);
    }

    [Fact]
    public void AddVariant_rejects_negative_price() =>
        Assert.Throws<DomainException>(() => Add(NewProduct(), price: -0.01m));

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 9)]
    public void AddVariant_rejects_compare_at_not_above_price(int price, int compareAt) =>
        Assert.Throws<DomainException>(() => Add(NewProduct(), price: price, compareAt: compareAt));

    [Fact]
    public void AddVariant_accepts_compare_at_above_price()
    {
        var p = NewProduct();
        Add(p, price: 10m, compareAt: 12.50m);
        Assert.Equal(12.50m, p.Variants[0].CompareAtPrice);
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("USD\n")]
    [InlineData("")]
    public void AddVariant_rejects_bad_currency(string currency) =>
        Assert.Throws<DomainException>(() => Add(NewProduct(), currency: currency));

    [Fact]
    public void AddVariant_rejects_second_variant_with_a_different_currency()
    {
        var p = NewProduct();
        Add(p, sku: "A-1", currency: "USD");
        Assert.Throws<DomainException>(() => Add(p, sku: "A-2", currency: "EUR"));
        Assert.Single(p.Variants);
    }

    [Fact]
    public void AddVariant_rejects_duplicate_sku_within_the_product()
    {
        var p = NewProduct();
        Add(p, sku: "A-1");
        Assert.Throws<DomainException>(() => Add(p, sku: "A-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("lower")]
    [InlineData("HAS SPACE")]
    [InlineData("SKU-1\n")]
    public void AddVariant_rejects_bad_sku(string sku) =>
        Assert.Throws<DomainException>(() => Add(NewProduct(), sku: sku));

    [Fact]
    public void AddVariant_enforces_sku_length_of_64()
    {
        Assert.Throws<DomainException>(() => Add(NewProduct(), sku: new string('A', 65)));
        Add(NewProduct(), sku: new string('A', 64));
    }

    [Fact]
    public void AddVariant_rejects_negative_weight() =>
        Assert.Throws<DomainException>(() => Add(NewProduct(), weight: -1));
}
