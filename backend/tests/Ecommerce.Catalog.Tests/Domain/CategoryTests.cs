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
    public void Rename_sets_slug_and_name_and_keeps_the_parent()
    {
        Guid parentId = Guid.NewGuid();
        Category category = Category.Create(Guid.NewGuid(), "tops", "Tops", parentId);

        category.Rename("shirts", "Shirts");

        Assert.Equal("shirts", category.Slug);
        Assert.Equal("Shirts", category.Name);
        Assert.Equal(parentId, category.ParentId);
    }

    [Fact]
    public void Rename_rejects_invalid_slug_and_blank_or_long_name_without_changing_anything()
    {
        Category category = Category.Create(Guid.NewGuid(), "tops", "Tops", null);

        Assert.Equal("Category slug is invalid.", Assert.Throws<DomainException>(() => category.Rename("Bad Slug", "Name")).Message);
        Assert.Throws<DomainException>(() => category.Rename("ok", " "));
        Assert.Throws<DomainException>(() => category.Rename("ok", new string('n', 101)));
        Assert.Equal("tops", category.Slug);
        Assert.Equal("Tops", category.Name);
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
