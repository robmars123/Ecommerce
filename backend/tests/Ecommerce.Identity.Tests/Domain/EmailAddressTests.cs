using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Tests.Domain;

// ID-3
public class EmailAddressTests
{
    [Fact]
    public void Normalize_trims_and_tolerates_null()
    {
        Assert.Equal("a@b.c", EmailAddress.Normalize("  a@b.c \t"));
        Assert.Equal("", EmailAddress.Normalize(null));
    }

    [Theory]
    [InlineData("a@b.c")]
    [InlineData("first.last+tag@sub.example.test")]
    public void Valid_addresses_are_accepted(string email) => Assert.True(EmailAddress.IsValid(email));

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.test")]
    [InlineData("a@b@c.test")]
    [InlineData("@example.test")]
    [InlineData("local@")]
    [InlineData("sp ace@example.test")]
    [InlineData("a@b.c\n")]
    [InlineData("a@b\u0007.c")]
    public void Invalid_addresses_are_rejected(string email) => Assert.False(EmailAddress.IsValid(email));

    [Fact]
    public void Length_limits_are_enforced()
    {
        string at254 = new string('a', 254 - "@b.c".Length) + "@b.c";
        string at255 = new string('a', 255 - "@b.c".Length) + "@b.c";
        Assert.Equal(254, at254.Length);

        Assert.True(EmailAddress.IsValid(at254));
        Assert.False(EmailAddress.IsValid(at255));
        Assert.False(EmailAddress.IsValid("ab"));
    }
}
