using Ecommerce.Catalog.Domain;
using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Catalog.Tests.Domain;

// Slice 1b domain rules: CD-1 (variant lengths), CD-2 (UpdateDetails), CD-3 (UpdateVariant),
// CD-4 (storage keys and image replacement), CD-5 (idempotent publish).
public class ProductAdminDomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddHours(1);
    private static readonly DateTimeOffset T2 = T0.AddHours(2);

    private static Product NewProduct() =>
        Product.Create(Guid.NewGuid(), "blue-shirt", "Blue Shirt", "A shirt", Guid.NewGuid(), T0);

    private static ProductVariant AddVariant(Product p, string sku = "BS-M", string name = "Medium", string? size = "M", string? colour = "Blue") =>
        p.AddVariant(Guid.NewGuid(), sku, name, size, colour, 10m, "USD", null, 100, T0);

    // ---- CD-1

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddVariant_rejects_a_blank_name(string name) =>
        Assert.Throws<DomainException>(() => AddVariant(NewProduct(), name: name));

    [Fact]
    public void AddVariant_name_length_boundary_is_200()
    {
        Assert.Throws<DomainException>(() => AddVariant(NewProduct(), name: new string('n', 201)));
        Assert.Equal(200, AddVariant(NewProduct(), name: new string('n', 200)).Name.Length);
    }

    [Fact]
    public void AddVariant_size_and_colour_boundary_is_50_and_null_is_allowed()
    {
        Assert.Throws<DomainException>(() => AddVariant(NewProduct(), size: new string('s', 51)));
        Assert.Throws<DomainException>(() => AddVariant(NewProduct(), colour: new string('c', 51)));
        AddVariant(NewProduct(), size: new string('s', 50), colour: new string('c', 50));
        ProductVariant v = AddVariant(NewProduct(), size: null, colour: null);
        Assert.Null(v.Size);
        Assert.Null(v.Colour);
    }

    [Fact]
    public void AddVariant_rejects_the_51st_variant()
    {
        Product p = NewProduct();
        for (int i = 0; i < Product.MaxVariants; i++)
        {
            AddVariant(p, sku: $"SKU-{i}");
        }

        Assert.Throws<DomainException>(() => AddVariant(p, sku: "SKU-OVER"));
        Assert.Equal(Product.MaxVariants, p.Variants.Count);
    }

    // ---- CD-2

    [Fact]
    public void UpdateDetails_changes_the_fields_and_UpdatedAt_while_inactive()
    {
        Product p = NewProduct();
        Guid category = Guid.NewGuid();

        p.UpdateDetails("red-shirt", "Red Shirt", "Now red", category, T1);

        Assert.Equal("red-shirt", p.Slug);
        Assert.Equal("Red Shirt", p.Name);
        Assert.Equal("Now red", p.Description);
        Assert.Equal(category, p.CategoryId);
        Assert.Equal(T1, p.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_rejects_a_slug_change_while_published_but_allows_the_same_slug()
    {
        Product p = NewProduct();
        AddVariant(p);
        p.Publish(T1);

        Assert.Throws<DomainException>(() => p.UpdateDetails("red-shirt", "Blue Shirt", "A shirt", p.CategoryId, T2));
        Assert.Equal("blue-shirt", p.Slug);

        p.UpdateDetails("blue-shirt", "Renamed", "A shirt", p.CategoryId, T2);
        Assert.Equal("Renamed", p.Name);
    }

    [Theory]
    [InlineData("Bad Slug", "Name")]
    [InlineData("ok-slug", "")]
    [InlineData("ok-slug", "   ")]
    public void UpdateDetails_applies_the_same_validation_as_Create(string slug, string name)
    {
        Product p = NewProduct();

        Assert.Throws<DomainException>(() => p.UpdateDetails(slug, name, "d", p.CategoryId, T1));
        Assert.Equal("blue-shirt", p.Slug);
    }

    [Fact]
    public void UpdateDetails_rejects_oversized_name_and_description_and_leaves_state_unchanged()
    {
        Product p = NewProduct();

        Assert.Throws<DomainException>(() => p.UpdateDetails("ok", new string('n', 201), "d", p.CategoryId, T1));
        Assert.Throws<DomainException>(() => p.UpdateDetails("ok", "n", new string('d', 4001), p.CategoryId, T1));

        Assert.Equal("Blue Shirt", p.Name);
        Assert.Equal(T0, p.UpdatedAt);
    }

    // ---- CD-3

    [Fact]
    public void UpdateVariant_changes_the_editable_fields_but_never_sku_or_currency()
    {
        Product p = NewProduct();
        ProductVariant v = AddVariant(p);

        p.UpdateVariant(v.Id, "Large", "L", null, 12.5m, 20m, 300, true, T1);

        Assert.Equal("BS-M", v.Sku);
        Assert.Equal("USD", v.Currency);
        Assert.Equal("Large", v.Name);
        Assert.Equal("L", v.Size);
        Assert.Null(v.Colour);
        Assert.Equal(12.5m, v.Price);
        Assert.Equal(20m, v.CompareAtPrice);
        Assert.Equal(300, v.WeightGrams);
        Assert.Equal(T1, p.UpdatedAt);
    }

    [Fact]
    public void UpdateVariant_applies_price_compare_at_weight_and_length_rules()
    {
        Product p = NewProduct();
        ProductVariant v = AddVariant(p);

        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, "n", null, null, -1m, null, 1, true, T1));
        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, "n", null, null, 10m, 10m, 1, true, T1));
        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, "n", null, null, 10m, null, -1, true, T1));
        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, " ", null, null, 10m, null, 1, true, T1));
        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, new string('n', 201), null, null, 10m, null, 1, true, T1));
        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, "n", new string('s', 51), null, 10m, null, 1, true, T1));
        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, "n", null, new string('c', 51), 10m, null, 1, true, T1));
        Assert.Equal(10m, v.Price);
    }

    [Fact]
    public void UpdateVariant_with_an_unknown_id_throws() =>
        Assert.Throws<DomainException>(() => NewProduct().UpdateVariant(Guid.NewGuid(), "n", null, null, 1m, null, 0, true, T1));

    [Fact]
    public void Deactivating_the_last_active_variant_of_a_published_product_throws()
    {
        Product p = NewProduct();
        ProductVariant v = AddVariant(p);
        p.Publish(T1);

        Assert.Throws<DomainException>(() => p.UpdateVariant(v.Id, v.Name, v.Size, v.Colour, v.Price, null, 100, false, T2));
        Assert.True(v.IsActive);
    }

    [Fact]
    public void Deactivating_one_of_two_active_variants_of_a_published_product_works()
    {
        Product p = NewProduct();
        ProductVariant a = AddVariant(p, sku: "A-1");
        AddVariant(p, sku: "A-2");
        p.Publish(T1);

        p.UpdateVariant(a.Id, a.Name, a.Size, a.Colour, a.Price, null, 100, false, T2);

        Assert.False(a.IsActive);
        Assert.Equal(T2, p.UpdatedAt);
    }

    [Fact]
    public void Deactivating_the_last_active_variant_of_an_unpublished_product_works()
    {
        Product p = NewProduct();
        ProductVariant v = AddVariant(p);

        p.UpdateVariant(v.Id, v.Name, v.Size, v.Colour, v.Price, null, 100, false, T1);

        Assert.False(v.IsActive);
    }

    [Fact]
    public void Reactivating_a_variant_works()
    {
        Product p = NewProduct();
        ProductVariant v = AddVariant(p);
        v.Deactivate();

        p.UpdateVariant(v.Id, v.Name, v.Size, v.Colour, v.Price, null, 100, true, T1);

        Assert.True(v.IsActive);
    }

    // ---- CD-4

    [Theory]
    [InlineData("dev/a b.jpg")]
    [InlineData("products/x/1.jpg")]
    [InlineData("a.jpg")]
    public void StorageKey_accepts_plain_relative_keys(string key) => Assert.True(StorageKey.IsValid(key));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("../x")]
    [InlineData("a/../b")]
    [InlineData("a/./b")]
    [InlineData("/x")]
    [InlineData("a//b")]
    [InlineData("a/")]
    [InlineData("https://x/y.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("a\\b")]
    [InlineData("a\u0001b")]
    [InlineData("a\nb")]
    public void StorageKey_rejects_traversal_urls_backslashes_and_control_characters(string? key) =>
        Assert.False(StorageKey.IsValid(key));

    [Fact]
    public void StorageKey_length_boundary_is_512()
    {
        Assert.True(StorageKey.IsValid(new string('a', 512)));
        Assert.False(StorageKey.IsValid(new string('a', 513)));
    }

    [Fact]
    public void ReplaceImages_orders_by_array_index_and_returns_the_removed_images()
    {
        Product p = NewProduct();
        ProductImage old = p.AddImage(Guid.NewGuid(), "old/1.jpg", "old", 0, T0);

        IReadOnlyList<ProductImage> removed = p.ReplaceImages(
            [(Guid.NewGuid(), "products/x/b.jpg", "B"), (Guid.NewGuid(), "products/x/a.jpg", "A")], T1);

        Assert.Equal([old], removed);
        Assert.Equal(["products/x/b.jpg", "products/x/a.jpg"], p.Images.OrderBy(i => i.SortOrder).Select(i => i.StorageKey));
        Assert.Equal([0, 1], p.Images.Select(i => i.SortOrder).Order());
        Assert.Equal(T1, p.UpdatedAt);
    }

    [Fact]
    public void ReplaceImages_rejects_a_bad_key_a_long_alt_text_and_the_21st_image_without_changing_anything()
    {
        Product p = NewProduct();
        p.AddImage(Guid.NewGuid(), "keep/1.jpg", "keep", 0, T0);

        Assert.Throws<DomainException>(() => p.ReplaceImages([(Guid.NewGuid(), "../evil", "")], T1));
        Assert.Throws<DomainException>(() => p.ReplaceImages([(Guid.NewGuid(), "ok/1.jpg", new string('a', 301))], T1));
        List<(Guid, string, string)> tooMany = Enumerable.Range(0, Product.MaxImages + 1).Select(i => (Guid.NewGuid(), $"ok/{i}.jpg", "")).ToList();
        Assert.Throws<DomainException>(() => p.ReplaceImages(tooMany, T1));

        Assert.Equal(["keep/1.jpg"], p.Images.Select(i => i.StorageKey));
        Assert.Equal(T0, p.UpdatedAt);
    }

    [Fact]
    public void ReplaceImages_accepts_exactly_the_maximum_and_AddImage_uses_the_same_key_rule()
    {
        Product p = NewProduct();
        List<(Guid, string, string)> max = Enumerable.Range(0, Product.MaxImages).Select(i => (Guid.NewGuid(), $"ok/{i}.jpg", "")).ToList();

        p.ReplaceImages(max, T1);

        Assert.Equal(Product.MaxImages, p.Images.Count);
        Assert.Throws<DomainException>(() => NewProduct().AddImage(Guid.NewGuid(), "https://x/y.jpg", "", 0, T1));
    }

    // ---- CD-5

    [Fact]
    public void Publish_and_Unpublish_are_idempotent_and_do_not_bump_UpdatedAt_on_repeat()
    {
        Product p = NewProduct();
        AddVariant(p);

        p.Publish(T1);
        p.Publish(T2);
        Assert.True(p.IsActive);
        Assert.Equal(T1, p.UpdatedAt);

        p.Unpublish(T2);
        p.Unpublish(T2.AddHours(1));
        Assert.False(p.IsActive);
        Assert.Equal(T2, p.UpdatedAt);
    }

    [Fact]
    public void Publishing_an_already_published_product_with_no_active_variant_left_is_still_a_no_op()
    {
        // Unreachable through UpdateVariant (it refuses), but the idempotency rule must not depend on the variant check.
        Product p = NewProduct();
        ProductVariant v = AddVariant(p);
        p.Publish(T1);
        v.Deactivate();

        p.Publish(T2);

        Assert.True(p.IsActive);
        Assert.Equal(T1, p.UpdatedAt);
    }
}
