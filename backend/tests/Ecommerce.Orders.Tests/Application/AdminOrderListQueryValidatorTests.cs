using Ecommerce.Orders.Api;
using FluentValidation.Results;

namespace Ecommerce.Orders.Tests.Application;

public class AdminOrderListQueryValidatorTests
{
    private static readonly AdminOrderListQueryValidator Validator = new();

    private static ValidationResult Run(AdminOrderListQuery query) => Validator.Validate(query);

    private static AdminOrderListQuery Empty() => new(null, null, null, null, null, null, null);

    [Fact]
    public void An_empty_query_is_valid()
    {
        Assert.True(Run(Empty()).IsValid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Page_must_be_one_or_more(int page, bool valid)
    {
        Assert.Equal(valid, Run(Empty() with { Page = page }).IsValid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void Page_size_is_1_to_100(int pageSize, bool valid)
    {
        Assert.Equal(valid, Run(Empty() with { PageSize = pageSize }).IsValid);
    }

    [Theory]
    [InlineData("Paid", true)]
    [InlineData("PendingPayment", true)]
    [InlineData("paid", false)]
    [InlineData("3", false)]
    [InlineData("Bogus", false)]
    public void Status_is_an_exact_order_status_name(string status, bool valid)
    {
        Assert.Equal(valid, Run(Empty() with { Status = status }).IsValid);
    }

    [Fact]
    public void Order_number_is_1_to_32_characters_after_trimming()
    {
        Assert.True(Run(Empty() with { OrderNumber = new string('1', 32) }).IsValid);
        Assert.True(Run(Empty() with { OrderNumber = " " + new string('1', 32) + " " }).IsValid);
        Assert.False(Run(Empty() with { OrderNumber = new string('1', 33) }).IsValid);
        Assert.False(Run(Empty() with { OrderNumber = "   " }).IsValid);
    }

    [Fact]
    public void Customer_is_1_to_254_characters_after_trimming()
    {
        Assert.True(Run(Empty() with { Customer = new string('a', 254) }).IsValid);
        Assert.False(Run(Empty() with { Customer = new string('a', 255) }).IsValid);
        Assert.False(Run(Empty() with { Customer = "" }).IsValid);
    }

    [Fact]
    public void From_may_equal_to_but_not_follow_it()
    {
        DateOnly day = new DateOnly(2026, 6, 10);

        Assert.True(Run(Empty() with { From = day, To = day }).IsValid);
        Assert.True(Run(Empty() with { From = day }).IsValid);
        ValidationResult bad = Run(Empty() with { From = day.AddDays(1), To = day });
        Assert.False(bad.IsValid);
        Assert.Contains(bad.Errors, e => e.PropertyName == "From");
    }
}
