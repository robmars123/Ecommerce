using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class ProductCreateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_is_inactive_with_created_and_updated_equal_to_now()
    {
        var p = Product.Create(Guid.NewGuid(), "blue-shirt", "Blue Shirt", "d", Guid.NewGuid(), Now);
        Assert.False(p.IsActive);
        Assert.Equal(Now, p.CreatedAt);
        Assert.Equal(Now, p.UpdatedAt);
        Assert.Empty(p.Variants);
        Assert.Empty(p.Images);
    }

    [Theory]
    [InlineData("Bad Slug", "Name")]
    [InlineData("ok-slug", "")]
    [InlineData("ok-slug", "   ")]
    public void Create_rejects_invalid_slug_or_empty_name(string slug, string name) =>
        Assert.Throws<DomainException>(() => Product.Create(Guid.NewGuid(), slug, name, "d", Guid.NewGuid(), Now));

    [Fact]
    public void Create_rejects_overlong_name_and_description()
    {
        Assert.Throws<DomainException>(() => Product.Create(Guid.NewGuid(), "a", new string('x', 201), "d", Guid.NewGuid(), Now));
        Assert.Throws<DomainException>(() => Product.Create(Guid.NewGuid(), "a", "n", new string('x', 4001), Guid.NewGuid(), Now));
    }

    [Fact]
    public void AddImage_validates_key_and_alt_length_and_records_image()
    {
        var p = Product.Create(Guid.NewGuid(), "a", "n", "d", Guid.NewGuid(), Now);
        Assert.Throws<DomainException>(() => p.AddImage(Guid.NewGuid(), "", "alt", 0, Now));
        Assert.Throws<DomainException>(() => p.AddImage(Guid.NewGuid(), new string('k', 513), "alt", 0, Now));
        Assert.Throws<DomainException>(() => p.AddImage(Guid.NewGuid(), "k", new string('a', 301), 0, Now));
        var img = p.AddImage(Guid.NewGuid(), "dev/a.jpg", "alt", 2, Now.AddMinutes(1));
        Assert.Equal(p.Id, img.ProductId);
        Assert.Equal(2, img.SortOrder);
        Assert.Equal(Now.AddMinutes(1), p.UpdatedAt);
    }
}
