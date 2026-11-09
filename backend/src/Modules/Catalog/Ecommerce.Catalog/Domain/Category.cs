using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Domain;

internal sealed class Category
{
    public const int MaxNameLength = 100;

    private Category() { }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = "";
    public string Name { get; private set; } = "";
    public Guid? ParentId { get; private set; }

    public static Category Create(Guid id, string slug, string name, Guid? parentId)
    {
        Validate(slug, name);
        if (parentId == id)
        {
            throw new DomainException("A category cannot be its own parent.");
        }
        return new Category { Id = id, Slug = slug, Name = name, ParentId = parentId };
    }

    /// <summary>Changes slug and name. The parent is fixed at create.</summary>
    public void Rename(string slug, string name)
    {
        Validate(slug, name);
        Slug = slug;
        Name = name;
    }

    private static void Validate(string slug, string name)
    {
        if (!Domain.Slug.IsValid(slug))
        {
            throw new DomainException("Category slug is invalid.");
        }
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            throw new DomainException($"Category name must be 1 to {MaxNameLength} characters.");
        }
    }
}
