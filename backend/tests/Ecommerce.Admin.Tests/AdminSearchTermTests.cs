using Ecommerce.Admin.Application;

namespace Ecommerce.Admin.Tests;

// Slice P2.8 U1-U3: the q rules (trim, then 2-100 characters).
public class AdminSearchTermTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" a ")]
    public void U1_Rejects_missing_blank_and_too_short(string? raw)
    {
        Assert.False(AdminSearchTerm.TryParse(raw, out _, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void U1_Rejects_101_characters()
    {
        Assert.False(AdminSearchTerm.TryParse(new string('x', 101), out _, out string? error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void U2_Accepts_the_bounds_and_trims()
    {
        Assert.True(AdminSearchTerm.TryParse("ab", out string two, out string? e1));
        Assert.Equal("ab", two);
        Assert.Null(e1);
        Assert.True(AdminSearchTerm.TryParse(new string('x', 100), out string hundred, out _));
        Assert.Equal(100, hundred.Length);
        Assert.True(AdminSearchTerm.TryParse("  ab  ", out string trimmed, out _));
        Assert.Equal("ab", trimmed);
        Assert.True(AdminSearchTerm.TryParse("  " + new string('x', 100) + "  ", out string padded, out _));
        Assert.Equal(100, padded.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    [InlineData("   ")]
    public void U3_Error_text_names_q(string? raw)
    {
        AdminSearchTerm.TryParse(raw, out _, out string? error);
        Assert.StartsWith("q ", error);
    }
}
