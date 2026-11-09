using Ecommerce.Catalog.Api.Admin;
using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Persistence;

namespace Ecommerce.Catalog.Tests.Application;

// CA-2 (admin list criteria) and CA-3 (category service).
public class AdminCategoryAndCriteriaTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeCategoryStore : ICategoryWriteStore
    {
        public Dictionary<string, (Guid Id, Guid? ParentId)> Existing { get; } = [];
        public List<Category> Added { get; } = [];

        public Task<(Guid Id, Guid? ParentId)?> FindBySlugAsync(string slug, CancellationToken ct) =>
            Task.FromResult(Existing.TryGetValue(slug, out (Guid Id, Guid? ParentId) c) ? ((Guid, Guid?)?)c : null);

        public void Add(Category category) => Added.Add(category);

        public Dictionary<string, Category> Stored { get; } = [];
        public Dictionary<Guid, string> Slugs { get; } = [];
        public List<Category> Removed { get; } = [];

        public Task<Category?> GetForUpdateAsync(string slug, CancellationToken ct) =>
            Task.FromResult(Stored.TryGetValue(slug, out Category? c) ? c : null);

        public Task<string?> FindSlugByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Slugs.TryGetValue(id, out string? s) ? s : null);

        public void Remove(Category category) => Removed.Add(category);
    }

    private static (AdminCategoryService Service, FakeCategoryStore Store, AdminFakeUnitOfWork Uow) NewService()
    {
        FakeCategoryStore store = new FakeCategoryStore();
        AdminFakeUnitOfWork uow = new AdminFakeUnitOfWork();
        return (new AdminCategoryService(store, uow, new AdminFakeClock(AdminEnv.T0)), store, uow);
    }

    [Fact]
    public async Task A_top_level_category_is_created()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork _) = NewService();

        AdminResult<CategoryView> result = await service.CreateAsync(new CreateCategoryCommand(" tops ", " Tops ", null), Ct);

        CategoryView view = Assert.IsType<AdminResult<CategoryView>.Ok>(result).Value;
        Assert.Equal(new CategoryView("tops", "Tops", null), view);
        Assert.Null(Assert.Single(store.Added).ParentId);
    }

    [Fact]
    public async Task A_child_category_points_at_its_top_level_parent()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork _) = NewService();
        Guid parentId = Guid.NewGuid();
        store.Existing["tops"] = (parentId, null);

        AdminResult<CategoryView> result = await service.CreateAsync(new CreateCategoryCommand("shirts", "Shirts", "tops"), Ct);

        Assert.Equal("tops", Assert.IsType<AdminResult<CategoryView>.Ok>(result).Value.ParentSlug);
        Assert.Equal(parentId, Assert.Single(store.Added).ParentId);
    }

    [Fact]
    public async Task A_parent_that_is_itself_a_child_is_Invalid()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        store.Existing["shirts"] = (Guid.NewGuid(), Guid.NewGuid());

        AdminResult<CategoryView> result = await service.CreateAsync(new CreateCategoryCommand("linen", "Linen", "shirts"), Ct);

        AdminResult<CategoryView>.Invalid invalid = Assert.IsType<AdminResult<CategoryView>.Invalid>(result);
        Assert.Contains("parentSlug", invalid.Errors.Keys);
        Assert.Contains("one level", invalid.Errors["parentSlug"][0]);
        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task An_unknown_parent_is_Invalid()
    {
        (AdminCategoryService? service, FakeCategoryStore _, AdminFakeUnitOfWork _) = NewService();

        AdminResult<CategoryView> result = await service.CreateAsync(new CreateCategoryCommand("shirts", "Shirts", "ghost"), Ct);

        Assert.Contains("parentSlug", Assert.IsType<AdminResult<CategoryView>.Invalid>(result).Errors.Keys);
    }

    [Fact]
    public async Task A_duplicate_slug_is_a_Conflict()
    {
        (AdminCategoryService? service, FakeCategoryStore _, AdminFakeUnitOfWork? uow) = NewService();
        uow.FailOnSave = new UniqueConstraintViolationException("ux_categories_slug");

        AdminResult<CategoryView> result = await service.CreateAsync(new CreateCategoryCommand("tops", "Tops", null), Ct);

        Assert.Equal("slug", Assert.IsType<AdminResult<CategoryView>.Conflict>(result).Field);
    }

    [Fact]
    public async Task A_bad_slug_is_Invalid_and_never_reaches_the_store()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork _) = NewService();

        AdminResult<CategoryView> result = await service.CreateAsync(new CreateCategoryCommand("Bad Slug", "Tops", null), Ct);

        Assert.IsType<AdminResult<CategoryView>.Invalid>(result);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task Create_with_a_parent_deleted_mid_request_is_Invalid_on_parentSlug()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        store.Existing["tops"] = (Guid.NewGuid(), null);
        uow.FailOnSave = new ForeignKeyViolationException(AdminCategoryService.ParentForeignKey);

        AdminResult<CategoryView> result = await service.CreateAsync(new CreateCategoryCommand("shirts", "Shirts", "tops"), Ct);

        Assert.Contains("parentSlug", Assert.IsType<AdminResult<CategoryView>.Invalid>(result).Errors.Keys);
    }

    [Fact]
    public async Task Update_of_an_unknown_category_is_NotFound()
    {
        (AdminCategoryService? service, FakeCategoryStore _, AdminFakeUnitOfWork _) = NewService();

        AdminResult<CategoryView> result = await service.UpdateAsync(new UpdateCategoryCommand("ghost", "tops", "Tops"), Ct);

        Assert.IsType<AdminResult<CategoryView>.NotFound>(result);
    }

    [Fact]
    public async Task Update_renames_and_returns_the_parents_slug()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        Guid parentId = Guid.NewGuid();
        store.Slugs[parentId] = "shirts";
        Category child = Category.Create(Guid.NewGuid(), "linen", "Linen", parentId);
        store.Stored["linen"] = child;

        AdminResult<CategoryView> result = await service.UpdateAsync(new UpdateCategoryCommand("linen", " flax ", " Flax "), Ct);

        Assert.Equal(new CategoryView("flax", "Flax", "shirts"), Assert.IsType<AdminResult<CategoryView>.Ok>(result).Value);
        Assert.Equal("flax", child.Slug);
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task Update_to_a_taken_slug_is_a_Conflict_on_slug()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        store.Stored["pants"] = Category.Create(Guid.NewGuid(), "pants", "Pants", null);
        uow.FailOnSave = new UniqueConstraintViolationException("ux_categories_slug");

        AdminResult<CategoryView> result = await service.UpdateAsync(new UpdateCategoryCommand("pants", "shirts", "Pants"), Ct);

        Assert.Equal("slug", Assert.IsType<AdminResult<CategoryView>.Conflict>(result).Field);
    }

    [Fact]
    public async Task Delete_of_an_unknown_category_is_NotFound()
    {
        (AdminCategoryService? service, FakeCategoryStore _, AdminFakeUnitOfWork _) = NewService();

        Assert.IsType<AdminResult<CategoryView>.NotFound>(await service.DeleteAsync("ghost", Ct));
    }

    [Theory]
    [InlineData("fk_products_category_id", "products")]
    [InlineData("fk_categories_parent_id", "subcategories")]
    public async Task Delete_maps_the_two_known_foreign_keys_to_Conflict(string constraint, string field)
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        store.Stored["tops"] = Category.Create(Guid.NewGuid(), "tops", "Tops", null);
        uow.FailOnSave = new ForeignKeyViolationException(constraint);

        AdminResult<CategoryView> result = await service.DeleteAsync("tops", Ct);

        Assert.Equal(field, Assert.IsType<AdminResult<CategoryView>.Conflict>(result).Field);
    }

    [Fact]
    public async Task Update_of_a_category_deleted_mid_request_is_NotFound()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        store.Stored["pants"] = Category.Create(Guid.NewGuid(), "pants", "Pants", null);
        uow.FailOnSave = new ConcurrencyConflictException("x");

        Assert.IsType<AdminResult<CategoryView>.NotFound>(await service.UpdateAsync(new UpdateCategoryCommand("pants", "trousers", "Trousers"), Ct));
    }

    [Fact]
    public async Task Delete_of_a_category_deleted_mid_request_is_NotFound()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        store.Stored["tops"] = Category.Create(Guid.NewGuid(), "tops", "Tops", null);
        uow.FailOnSave = new ConcurrencyConflictException("x");

        Assert.IsType<AdminResult<CategoryView>.NotFound>(await service.DeleteAsync("tops", Ct));
    }

    [Fact]
    public async Task Delete_does_not_swallow_an_unrelated_foreign_key()
    {
        (AdminCategoryService? service, FakeCategoryStore? store, AdminFakeUnitOfWork? uow) = NewService();
        store.Stored["tops"] = Category.Create(Guid.NewGuid(), "tops", "Tops", null);
        uow.FailOnSave = new ForeignKeyViolationException("fk_something_else");

        await Assert.ThrowsAsync<ForeignKeyViolationException>(() => service.DeleteAsync("tops", Ct));
    }

    [Fact]
    public async Task UpdateCategoryRequestValidator_accepts_a_valid_body_and_rejects_the_create_messages()
    {
        UpdateCategoryRequestValidator validator = new UpdateCategoryRequestValidator();

        Assert.True((await validator.ValidateAsync(new UpdateCategoryRequest("tops", "Tops"), Ct)).IsValid);
        FluentValidation.Results.ValidationResult bad = await validator.ValidateAsync(new UpdateCategoryRequest("Bad Slug", new string('n', 101)), Ct);
        Assert.Equal(["Slug", "Name"], bad.Errors.Select(e => e.PropertyName));
        Assert.False((await validator.ValidateAsync(new UpdateCategoryRequest("tops", " "), Ct)).IsValid);
    }

    [Fact]
    public void Criteria_default_to_all_statuses_page_1_size_20()
    {
        AdminCriteriaResult result = AdminProductListCriteria.Create(null, null, null, null, null);

        Assert.True(result.IsValid);
        Assert.Equal(new AdminProductListCriteria(null, null, AdminProductStatus.All, 1, 20), result.Criteria);
    }

    [Theory]
    [InlineData("all", "All")]
    [InlineData("active", "Active")]
    [InlineData(" inactive ", "Inactive")]
    public void Criteria_parse_the_status(string status, string expected) =>
        Assert.Equal(expected, AdminProductListCriteria.Create(null, null, status, null, null).Criteria!.Status.ToString());

    [Fact]
    public void Criteria_report_every_out_of_range_value()
    {
        AdminCriteriaResult result = AdminProductListCriteria.Create(new string('x', 101), null, "archived", 0, 101);

        Assert.False(result.IsValid);
        Assert.Equal(["page", "pageSize", "search", "status"], result.Errors!.Keys.Order());
    }

    [Fact]
    public void Criteria_trim_search_and_category_and_accept_the_boundaries()
    {
        AdminCriteriaResult result = AdminProductListCriteria.Create("  shirt ", " tops ", null, 2, 100);

        Assert.Equal(new AdminProductListCriteria("shirt", "tops", AdminProductStatus.All, 2, 100), result.Criteria);
        Assert.True(AdminProductListCriteria.Create(new string('x', 100), null, null, 1, 1).IsValid);
    }
}
