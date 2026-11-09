using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Domain;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Catalog.Application.Admin;

internal sealed class AdminCategoryService(ICategoryWriteStore categories, IUnitOfWork unitOfWork, IClock clock)
{
    public const string SlugConstraint = "ux_categories_slug";
    public const string ParentForeignKey = "fk_categories_parent_id";
    public const string ProductsForeignKey = "fk_products_category_id";

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
        catch (ForeignKeyViolationException ex) when (ex.ConstraintName == ParentForeignKey)
        {
            return AdminResult<CategoryView>.Invalid.For("parentSlug", "That parent category does not exist.");
        }
        return new AdminResult<CategoryView>.Ok(new CategoryView(category.Slug, category.Name, parentSlug));
    }

    public async Task<AdminResult<CategoryView>> UpdateAsync(UpdateCategoryCommand command, CancellationToken ct)
    {
        string slug = command.Slug.Trim();
        string name = command.Name.Trim();

        Category? category = await categories.GetForUpdateAsync(command.CurrentSlug, ct);
        if (category is null)
        {
            return new AdminResult<CategoryView>.NotFound();
        }

        try
        {
            category.Rename(slug, name);
        }
        catch (DomainException ex)
        {
            return AdminResult<CategoryView>.Invalid.For("category", ex.Message);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == SlugConstraint)
        {
            return new AdminResult<CategoryView>.Conflict("slug", "A category with this slug already exists.");
        }
        catch (ConcurrencyConflictException)
        {
            return new AdminResult<CategoryView>.NotFound();
        }
        string? parentSlug = category.ParentId is null ? null : await categories.FindSlugByIdAsync(category.ParentId.Value, ct);
        return new AdminResult<CategoryView>.Ok(new CategoryView(category.Slug, category.Name, parentSlug));
    }

    public async Task<AdminResult<CategoryView>> DeleteAsync(string slug, CancellationToken ct)
    {
        Category? category = await categories.GetForUpdateAsync(slug, ct);
        if (category is null)
        {
            return new AdminResult<CategoryView>.NotFound();
        }

        categories.Remove(category);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ForeignKeyViolationException ex) when (ex.ConstraintName == ProductsForeignKey)
        {
            return new AdminResult<CategoryView>.Conflict("products", "This category still has products. Move them to another category first.");
        }
        catch (ForeignKeyViolationException ex) when (ex.ConstraintName == ParentForeignKey)
        {
            return new AdminResult<CategoryView>.Conflict("subcategories", "This category has subcategories. Delete them first.");
        }
        catch (ConcurrencyConflictException)
        {
            return new AdminResult<CategoryView>.NotFound();
        }
        return new AdminResult<CategoryView>.Ok(new CategoryView(category.Slug, category.Name, null));
    }
}
