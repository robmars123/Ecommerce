using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

public class CategoryTests
{
    [Fact]
    public void Create_accepts_valid_category_with_and_without_parent()
    {
        Category parent = Category.Create(Guid.NewGuid(), "clothing", "Clothing", null);
        Category child = Category.Create(Guid.NewGuid(), "tops", "Tops", parent.Id);
        Assert.Null(parent.ParentId);
        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public void Create_rejects_invalid_slug_empty_name_and_self_parent()
    {
        Guid id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => Category.Create(id, "Bad Slug", "Name", null));
        Assert.Throws<DomainException>(() => Category.Create(id, "ok", "", null));
        Assert.Throws<DomainException>(() => Category.Create(id, "ok", new string('n', 101), null));
        Assert.Throws<DomainException>(() => Category.Create(id, "ok", "Name", id));
    }
}
