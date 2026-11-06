using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Catalog.Application.Admin;

internal sealed class AdminCategoryService(ICategoryWriteStore categories, IUnitOfWork unitOfWork, IClock clock)
{
    public const string SlugConstraint = "ux_categories_slug";

    public async Task<AdminResult<CategoryView>> CreateAsync(CreateCategoryCommand command, CancellationToken ct)
    {
        string slug = command.Slug.Trim();
        string name = command.Name.Trim();
        string? parentSlug = string.IsNullOrWhiteSpace(command.ParentSlug) ? null : command.ParentSlug.Trim();

        Guid? parentId = null;
        if (parentSlug is not null)
        {
            (Guid Id, Guid? ParentId)? parent = await categories.FindBySlugAsync(parentSlug, ct);
            if (parent is null)
            {
                return AdminResult<CategoryView>.Invalid.For("parentSlug", "That parent category does not exist.");
            }
            if (parent.Value.ParentId is not null)
            {
                return AdminResult<CategoryView>.Invalid.For("parentSlug", "Categories can be nested one level only.");
            }
            parentId = parent.Value.Id;
        }

        Category category;
        try
        {
            category = Category.Create(Guid.CreateVersion7(clock.UtcNow), slug, name, parentId);
        }
        catch (DomainException ex)
        {
            return AdminResult<CategoryView>.Invalid.For("category", ex.Message);
        }

        categories.Add(category);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == SlugConstraint)
        {
            return new AdminResult<CategoryView>.Conflict("slug", "A category with this slug already exists.");
        }
        return new AdminResult<CategoryView>.Ok(new CategoryView(category.Slug, category.Name, parentSlug));
    }
}
