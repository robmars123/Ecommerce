using Ecommerce.Catalog.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class SlugTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("blue-shirt")]
    [InlineData("x1-2")]
    public void Accepts_canonical_slugs(string value) => Assert.True(Slug.IsValid(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Blue-Shirt")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a--b")]
    [InlineData("a b")]
    [InlineData("a_b")]
    [InlineData("blue-shirt\n")]
    public void Rejects_non_canonical_slugs(string? value) => Assert.False(Slug.IsValid(value));

    [Fact]
    public void Enforces_100_character_limit()
    {
        Assert.True(Slug.IsValid(new string('a', 100)));
        Assert.False(Slug.IsValid(new string('a', 101)));
    }
}
