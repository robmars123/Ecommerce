using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Persistence;

namespace Ecommerce.Catalog.Tests.Application;

// CA-1: the admin write service, with fakes.
public class AdminProductServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static T Is<T>(object value) where T : class => Assert.IsType<T>(value);

    // ---- create

    [Fact]
    public async Task Create_with_an_unknown_category_is_Invalid_on_categorySlug_and_saves_nothing()
    {
        AdminEnv env = new AdminEnv();

        AdminResult<AdminProductView> result = await env.Service.CreateAsync(AdminEnv.CreateCommand(category: "nope"), Ct);

        AdminResult<AdminProductView>.Invalid invalid = Is<AdminResult<AdminProductView>.Invalid>(result);
        Assert.Contains("categorySlug", invalid.Errors.Keys);
        Assert.Equal(0, env.UnitOfWork.Saves);
        Assert.Empty(env.Writes.Products);
    }

    [Fact]
    public async Task Create_makes_an_inactive_product_with_exactly_the_prices_given_and_returns_the_stored_view()
    {
        AdminEnv env = new AdminEnv();

        AdminResult<AdminProductView> result = await env.Service.CreateAsync(
            AdminEnv.CreateCommand(variants: AdminEnv.Variant(price: 49.9m)), Ct);

        AdminProductView view = Is<AdminResult<AdminProductView>.Ok>(result).Value;
        Assert.False(view.IsActive);
        Assert.Equal(49.9m, Assert.Single(view.Variants).Price);
        Assert.Equal(1, env.UnitOfWork.Saves);
        Assert.False(Assert.Single(env.Writes.Products.Values).IsActive);
        Assert.Empty(env.Inventory.Calls);
    }

    [Fact]
    public async Task Create_trims_text_and_turns_empty_size_and_colour_into_null()
    {
        AdminEnv env = new AdminEnv();
        VariantInput variant = new VariantInput(null, "LS-M", "  Medium  ", "  ", "", 10m, "USD", null, 1, true);

        AdminResult<AdminProductView> result = await env.Service.CreateAsync(
            new CreateProductCommand("linen-shirt", "  Linen Shirt ", "  d ", "tops", [variant], []), Ct);

        AdminProductView view = Is<AdminResult<AdminProductView>.Ok>(result).Value;
        Assert.Equal("Linen Shirt", view.Name);
        AdminVariantRow v = Assert.Single(view.Variants);
        Assert.Equal("Medium", v.Name);
        Assert.Null(v.Size);
        Assert.Null(v.Colour);
    }

    [Fact]
    public async Task Create_maps_a_duplicate_slug_to_Conflict_on_slug()
    {
        AdminEnv env = new AdminEnv();
        env.UnitOfWork.FailOnSave = new UniqueConstraintViolationException("ux_products_slug");

        AdminResult<AdminProductView> result = await env.Service.CreateAsync(AdminEnv.CreateCommand(), Ct);

        Assert.Equal("slug", Is<AdminResult<AdminProductView>.Conflict>(result).Field);
    }

    [Fact]
    public async Task Create_with_a_category_deleted_mid_request_is_Invalid_on_categorySlug()
    {
        AdminEnv env = new AdminEnv();
        env.UnitOfWork.FailOnSave = new ForeignKeyViolationException("fk_products_category_id");

        AdminResult<AdminProductView> result = await env.Service.CreateAsync(AdminEnv.CreateCommand(), Ct);

        Assert.Contains("categorySlug", Is<AdminResult<AdminProductView>.Invalid>(result).Errors.Keys);
    }

    [Fact]
    public async Task Update_with_a_category_deleted_mid_request_is_Invalid_on_categorySlug()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        env.UnitOfWork.FailOnSave = new ForeignKeyViolationException("fk_products_category_id");

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product), Ct);

        Assert.Contains("categorySlug", Is<AdminResult<AdminProductView>.Invalid>(result).Errors.Keys);
    }

    [Fact]
    public async Task Create_maps_a_duplicate_sku_to_Conflict_on_variants()
    {
        AdminEnv env = new AdminEnv();
        env.UnitOfWork.FailOnSave = new UniqueConstraintViolationException("ux_product_variants_sku");

        AdminResult<AdminProductView> result = await env.Service.CreateAsync(AdminEnv.CreateCommand(), Ct);

        Assert.Equal("variants", Is<AdminResult<AdminProductView>.Conflict>(result).Field);
    }

    [Fact]
    public async Task Create_does_not_swallow_an_unrelated_unique_violation()
    {
        AdminEnv env = new AdminEnv();
        env.UnitOfWork.FailOnSave = new UniqueConstraintViolationException("ux_something_else");

        await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => env.Service.CreateAsync(AdminEnv.CreateCommand(), Ct));
    }

    [Fact]
    public async Task Create_turns_a_domain_rule_violation_into_Invalid()
    {
        AdminEnv env = new AdminEnv();

        // Two variants with different currencies break the one-currency rule.
        AdminResult<AdminProductView> result = await env.Service.CreateAsync(
            AdminEnv.CreateCommand(variants: [AdminEnv.Variant("A-1"), AdminEnv.Variant("A-2", currency: "EUR")]), Ct);

        Is<AdminResult<AdminProductView>.Invalid>(result);
        Assert.Equal(0, env.UnitOfWork.Saves);
    }

    // ---- update

    [Fact]
    public async Task Update_of_an_unknown_product_is_NotFound()
    {
        AdminEnv env = new AdminEnv();

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(Guid.NewGuid(), AdminEnv.UpdateCommand(env.SeedProduct()), Ct);

        Is<AdminResult<AdminProductView>.NotFound>(result);
    }

    [Fact]
    public async Task Update_sets_the_expected_version_before_saving_and_replaces_images_in_array_order()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        product.AddImage(Guid.NewGuid(), "old/1.jpg", "", 0, AdminEnv.T0);

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, version: 812345), Ct);

        Is<AdminResult<AdminProductView>.Ok>(result);
        Assert.Equal((product, 812345u), Assert.Single(env.Writes.ExpectedVersions));
        Assert.Equal(["products/x/2.jpg"], product.Images.Select(i => i.StorageKey));
        Assert.Equal(["old/1.jpg"], env.Writes.RemovedImages.Select(i => i.StorageKey));
    }

    [Fact]
    public async Task Update_maps_a_concurrency_conflict_to_Conflict_on_version()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        env.UnitOfWork.FailOnSave = new ConcurrencyConflictException("stale");

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product), Ct);

        AdminResult<AdminProductView>.Conflict conflict = Is<AdminResult<AdminProductView>.Conflict>(result);
        Assert.Equal("version", conflict.Field);
        Assert.Contains("changed by someone else", conflict.Message);
    }

    [Fact]
    public async Task Update_that_omits_an_existing_variant_is_Invalid_and_saves_nothing()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct(false, "A-1", "A-2");
        VariantInput[] keepOnlyFirst = new[] { AdminEnv.Variant("A-1", product.Variants[0].Id) };

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, variants: keepOnlyFirst), Ct);

        AdminResult<AdminProductView>.Invalid invalid = Is<AdminResult<AdminProductView>.Invalid>(result);
        Assert.Contains("variants", invalid.Errors.Keys);
        Assert.Equal(0, env.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Update_with_a_variant_id_from_another_product_is_Invalid()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        VariantInput[] foreign = new[] { AdminEnv.Variant("LS-M", product.Variants[0].Id), AdminEnv.Variant("X-1", Guid.NewGuid()) };

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, variants: foreign), Ct);

        Is<AdminResult<AdminProductView>.Invalid>(result);
        Assert.Equal(0, env.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Update_cannot_change_the_sku_or_currency_of_an_existing_variant()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        Guid id = product.Variants[0].Id;

        AdminResult<AdminProductView> skuChange = await env.Service.UpdateAsync(
            product.Id, AdminEnv.UpdateCommand(product, variants: [AdminEnv.Variant("OTHER-SKU", id)]), Ct);
        AdminResult<AdminProductView> currencyChange = await env.Service.UpdateAsync(
            product.Id, AdminEnv.UpdateCommand(product, variants: [AdminEnv.Variant("LS-M", id, currency: "EUR")]), Ct);

        Is<AdminResult<AdminProductView>.Invalid>(skuChange);
        Is<AdminResult<AdminProductView>.Invalid>(currencyChange);
        Assert.Equal("LS-M", product.Variants[0].Sku);
    }

    [Fact]
    public async Task Update_adds_a_new_variant_without_an_id()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        List<VariantInput> variants = product.Variants.Select(v => AdminEnv.Variant(v.Sku, v.Id)).Append(AdminEnv.Variant("LS-L")).ToList();

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, variants: variants), Ct);

        Is<AdminResult<AdminProductView>.Ok>(result);
        Assert.Equal(2, product.Variants.Count);
    }

    [Fact]
    public async Task Update_with_a_new_variant_in_another_currency_is_Invalid()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        List<VariantInput> variants = product.Variants.Select(v => AdminEnv.Variant(v.Sku, v.Id)).Append(AdminEnv.Variant("LS-L", currency: "EUR")).ToList();

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, variants: variants), Ct);

        Is<AdminResult<AdminProductView>.Invalid>(result);
    }

    [Fact]
    public async Task Update_changing_the_slug_of_a_published_product_is_Invalid_on_slug()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct(published: true);

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, slug: "renamed"), Ct);

        Assert.Contains("slug", Is<AdminResult<AdminProductView>.Invalid>(result).Errors.Keys);
        Assert.Equal("linen-shirt", product.Slug);
    }

    [Fact]
    public async Task Update_deactivating_the_last_active_variant_of_a_published_product_is_a_Conflict()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct(published: true);
        Guid id = product.Variants[0].Id;

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(
            product.Id, AdminEnv.UpdateCommand(product, variants: [AdminEnv.Variant("LS-M", id, active: false)]), Ct);

        Is<AdminResult<AdminProductView>.Conflict>(result);
        Assert.True(product.Variants[0].IsActive);
        Assert.Equal(0, env.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Update_may_swap_which_variant_is_active_in_one_request()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct(published: true, "A-1", "A-2");
        product.UpdateVariant(product.Variants[1].Id, "Medium", "M", null, 49m, null, 250, false, AdminEnv.T0);
        VariantInput[] swapped = new[]
        {
            AdminEnv.Variant("A-1", product.Variants[0].Id, active: false),
            AdminEnv.Variant("A-2", product.Variants[1].Id, active: true),
        };

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, variants: swapped), Ct);

        Is<AdminResult<AdminProductView>.Ok>(result);
        Assert.False(product.Variants[0].IsActive);
        Assert.True(product.Variants[1].IsActive);
    }

    [Fact]
    public async Task Update_maps_a_duplicate_slug_to_Conflict_on_slug()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        env.UnitOfWork.FailOnSave = new UniqueConstraintViolationException("ux_products_slug");

        AdminResult<AdminProductView> result = await env.Service.UpdateAsync(product.Id, AdminEnv.UpdateCommand(product, slug: "taken"), Ct);

        Assert.Equal("slug", Is<AdminResult<AdminProductView>.Conflict>(result).Field);
    }

    // ---- publish / unpublish / preview

    [Fact]
    public async Task Publish_with_no_active_variant_is_a_Conflict()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        product.UpdateVariant(product.Variants[0].Id, "Medium", "M", null, 49m, null, 250, false, AdminEnv.T0);

        AdminResult<AdminProductView> result = await env.Service.PublishAsync(product.Id, Ct);

        Is<AdminResult<AdminProductView>.Conflict>(result);
        Assert.False(product.IsActive);
    }

    [Fact]
    public async Task Publish_then_unpublish_toggle_visibility_and_NotFound_for_unknown_ids()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();

        Assert.True(Is<AdminResult<AdminProductView>.Ok>(await env.Service.PublishAsync(product.Id, Ct)).Value.IsActive);
        Assert.False(Is<AdminResult<AdminProductView>.Ok>(await env.Service.UnpublishAsync(product.Id, Ct)).Value.IsActive);
        Is<AdminResult<AdminProductView>.NotFound>(await env.Service.PublishAsync(Guid.NewGuid(), Ct));
        Is<AdminResult<AdminProductView>.NotFound>(await env.Service.UnpublishAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Publish_reports_a_concurrency_conflict_as_Conflict()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();
        env.UnitOfWork.FailOnSave = new ConcurrencyConflictException("stale");

        Is<AdminResult<AdminProductView>.Conflict>(await env.Service.PublishAsync(product.Id, Ct));
    }

    [Fact]
    public async Task Preview_makes_exactly_one_inventory_call_for_the_active_variants()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct(false, "A-1", "A-2");
        product.UpdateVariant(product.Variants[1].Id, "Medium", "M", null, 49m, null, 250, false, AdminEnv.T0);

        AdminResult<AdminPreview> result = await env.Service.PreviewAsync(product.Id, Ct);

        AdminPreview preview = Is<AdminResult<AdminPreview>.Ok>(result).Value;
        Assert.Single(env.Inventory.Calls);
        Assert.Equal([product.Variants[0].Id], Assert.Single(env.Inventory.Calls));
        Assert.False(preview.IsActive);
        Assert.False(preview.IsVisibleToCustomers);
        Assert.Single(preview.Product.Variants);
    }

    [Fact]
    public async Task Preview_of_a_published_product_is_visible_to_customers_and_unknown_ids_are_NotFound()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct(published: true);

        Assert.True(Is<AdminResult<AdminPreview>.Ok>(await env.Service.PreviewAsync(product.Id, Ct)).Value.IsVisibleToCustomers);
        Is<AdminResult<AdminPreview>.NotFound>(await env.Service.PreviewAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Get_returns_the_view_or_NotFound()
    {
        AdminEnv env = new AdminEnv();
        Product product = env.SeedProduct();

        Assert.Equal(product.Id, Is<AdminResult<AdminProductView>.Ok>(await env.Service.GetAsync(product.Id, Ct)).Value.Id);
        Is<AdminResult<AdminProductView>.NotFound>(await env.Service.GetAsync(Guid.NewGuid(), Ct));
    }
}
