using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class ProductPublishTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Product NewProduct() =>
        Product.Create(Guid.NewGuid(), "blue-shirt", "Blue Shirt", "A shirt", Guid.NewGuid(), T0);

    [Fact]
    public void Publish_without_any_variant_throws_and_stays_inactive()
    {
        Product p = NewProduct();
        Assert.Throws<DomainException>(() => p.Publish(T0.AddHours(1)));
        Assert.False(p.IsActive);
        Assert.Equal(T0, p.UpdatedAt);
    }

    [Fact]
    public void Publish_with_only_deactivated_variants_throws()
    {
        Product p = NewProduct();
        p.AddVariant(Guid.NewGuid(), "A-1", "One", null, null, 5m, "USD", null, 0, T0).Deactivate();
        Assert.Throws<DomainException>(() => p.Publish(T0.AddHours(1)));
        Assert.False(p.IsActive);
    }

    [Fact]
    public void Publish_with_an_active_variant_sets_active_and_updated_at_then_Unpublish_clears()
    {
        Product p = NewProduct();
        p.AddVariant(Guid.NewGuid(), "A-1", "One", null, null, 5m, "USD", null, 0, T0);

        p.Publish(T0.AddHours(2));
        Assert.True(p.IsActive);
        Assert.Equal(T0.AddHours(2), p.UpdatedAt);

        p.Unpublish(T0.AddHours(3));
        Assert.False(p.IsActive);
        Assert.Equal(T0.AddHours(3), p.UpdatedAt);
    }
}
