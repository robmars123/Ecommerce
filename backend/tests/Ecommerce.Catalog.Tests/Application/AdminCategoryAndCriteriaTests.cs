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
