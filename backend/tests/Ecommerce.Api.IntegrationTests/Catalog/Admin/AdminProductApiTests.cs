using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Catalog.Admin;

// IA-1 .. IA-7: admin catalog over HTTP with a real Postgres.
public class AdminProductApiTests(PostgresFixture postgres)
{
    private static string Url(IdentityTestEnv.Api product) => $"{AdminSamples.Products}/{product.Str("id")}";

    private static async Task<(IdentityTestEnv Env, string Token)> StartAsync(PostgresFixture postgres)
    {
        IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        return (env, await AdminSamples.AdminTokenAsync(env));
    }

    [Fact]
    public async Task IA1_create_returns_201_with_location_an_inactive_product_and_stays_hidden_publicly()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;

        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);

        Assert.Equal(Url(created), created.Response.Headers.Location?.OriginalString);
        Assert.False(created.Json.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync("/api/v1/products/linen-shirt")).Status);
        Assert.Empty((await env.GetAsync("/api/v1/products")).Json.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task IA1_duplicates_unknown_category_extra_members_and_bad_prices_are_rejected()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        await AdminSamples.CreateProductAsync(env, token);

        IdentityTestEnv.Api dupSlug = await env.PostAsync(AdminSamples.Products, AdminSamples.Product("linen-shirt", "OTHER-1"), token);
        Assert.Equal(HttpStatusCode.Conflict, dupSlug.Status);
        Assert.True(dupSlug.Json.GetProperty("errors").TryGetProperty("slug", out _));

        IdentityTestEnv.Api dupSku = await env.PostAsync(AdminSamples.Products, AdminSamples.Product("another", "LS-1"), token);
        Assert.Equal(HttpStatusCode.Conflict, dupSku.Status);

        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(AdminSamples.Products, AdminSamples.Product("x1", "X-1", category: "nope"), token)).Status);

        string extra = """{"slug":"x2","name":"X","description":"","categorySlug":"shirts","isActive":true,"variants":[],"images":[]}""";
        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(AdminSamples.Products, extra, token)).Status);

        foreach (string? amount in new[] { "-1", "1.23456", "abc" })
        {
            object body = AdminSamples.Product("p-" + amount.Length, "P-" + amount.Length + amount[0], variants: [AdminSamples.Variant("P-" + amount.Length + amount[0], amount)]);
            Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(AdminSamples.Products, body, token)).Status);
        }
        string numeric = """{"slug":"x3","name":"X","description":"","categorySlug":"shirts","variants":[{"sku":"N-1","name":"D","size":null,"colour":null,"price":{"amount":1.5,"currency":"USD"},"compareAtPrice":null,"weightGrams":1}],"images":[]}""";
        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(AdminSamples.Products, numeric, token)).Status);

        object longBody = new { slug = "x4", name = new string('n', 201), description = "", categorySlug = "shirts", variants = Array.Empty<object>(), images = Array.Empty<object>() };
        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(AdminSamples.Products, longBody, token)).Status);
    }

    [Fact]
    public async Task IA2_a_stale_version_is_409_on_version()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);

        IdentityTestEnv.Api first = await env.SendAsync(HttpMethod.Put, Url(created), AdminSamples.UpdateBody(created.Json, name: "First"), token);
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.NotEqual(created.Json.GetProperty("version").GetUInt32(), first.Json.GetProperty("version").GetUInt32());

        IdentityTestEnv.Api stale = await env.SendAsync(HttpMethod.Put, Url(created), AdminSamples.UpdateBody(created.Json, name: "Second"), token);
        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.True(stale.Json.GetProperty("errors").TryGetProperty("version", out _));
    }

    [Fact]
    public async Task IA2_two_parallel_updates_from_one_version_produce_one_200_and_one_409_even_with_a_frozen_clock()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres); // FakeClock never advances in this test
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);

        IdentityTestEnv.Api[] results = await Task.WhenAll(
            env.SendAsync(HttpMethod.Put, Url(created), AdminSamples.UpdateBody(created.Json, name: "A"), token),
            env.SendAsync(HttpMethod.Put, Url(created), AdminSamples.UpdateBody(created.Json, name: "B"), token));

        Assert.Single(results, r => r.Status == HttpStatusCode.OK);
        Assert.Single(results, r => r.Status == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task IA2_two_parallel_edits_that_change_only_a_variant_price_produce_one_200_and_one_409_on_version()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres); // frozen FakeClock: updated_at cannot differ between the two writes
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);

        object Body(string price)
        {
            JsonElement v = created.Json.GetProperty("variants")[0];
            return new
            {
                version = created.Json.GetProperty("version").GetUInt32(),
                slug = created.Json.GetProperty("slug").GetString(),
                name = created.Json.GetProperty("name").GetString(),
                description = created.Json.GetProperty("description").GetString(),
                categorySlug = "shirts",
                variants = new[]
                {
                    new
                    {
                        id = (Guid?)v.GetProperty("id").GetGuid(),
                        sku = v.GetProperty("sku").GetString(),
                        name = v.GetProperty("name").GetString(),
                        size = (string?)null,
                        colour = (string?)null,
                        price = new { amount = price, currency = "USD" },
                        compareAtPrice = (object?)null,
                        weightGrams = v.GetProperty("weightGrams").GetInt32(),
                        isActive = v.GetProperty("isActive").GetBoolean(),
                    },
                },
                images = Array.Empty<object>(),
            };
        }

        IdentityTestEnv.Api[] results = await Task.WhenAll(
            env.SendAsync(HttpMethod.Put, Url(created), Body("10.00"), token),
            env.SendAsync(HttpMethod.Put, Url(created), Body("20.00"), token));

        Assert.Single(results, r => r.Status == HttpStatusCode.OK);
        IdentityTestEnv.Api loser = Assert.Single(results, r => r.Status == HttpStatusCode.Conflict);
        Assert.True(loser.Json.GetProperty("errors").TryGetProperty("version", out _));
    }

    [Fact]
    public async Task IA3_update_rules_no_removal_by_omission_slug_lock_last_active_variant_currency_and_image_order()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);
        string url = Url(created);

        // Omitting the existing variant is a 400.
        object noVariants = new { version = created.Json.GetProperty("version").GetUInt32(), slug = "linen-shirt", name = "Linen shirt", description = "", categorySlug = "shirts", variants = Array.Empty<object>(), images = Array.Empty<object>() };
        Assert.Equal(HttpStatusCode.BadRequest, (await env.SendAsync(HttpMethod.Put, url, noVariants, token)).Status);

        // The currency cannot change.
        Assert.Equal(HttpStatusCode.BadRequest, (await env.SendAsync(HttpMethod.Put, url, AdminSamples.UpdateBody(created.Json, currency: "EUR"), token)).Status);

        // Images are stored in the order given.
        IdentityTestEnv.Api withImages = await env.SendAsync(HttpMethod.Put, url,
            AdminSamples.UpdateBody(created.Json, images: [new { storageKey = "dev/b.jpg", altText = "B" }, new { storageKey = "dev/a.jpg", altText = "A" }]), token);
        Assert.Equal(HttpStatusCode.OK, withImages.Status);
        string?[] keys = withImages.Json.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("storageKey").GetString()).ToArray();
        Assert.Equal(new[] { "dev/b.jpg", "dev/a.jpg" }, keys);

        // Activate the variant and publish, then the slug is locked and the last active variant cannot go.
        IdentityTestEnv.Api active = await env.SendAsync(HttpMethod.Put, url, AdminSamples.UpdateBody(withImages.Json, variantActive: true), token);
        Assert.Equal(HttpStatusCode.OK, active.Status);
        Assert.Equal(HttpStatusCode.OK, (await env.PostAsync(url + "/publish", null, token)).Status);
        JsonElement current = (await env.GetAsync(url, token)).Json.Clone();

        Assert.Equal(HttpStatusCode.BadRequest, (await env.SendAsync(HttpMethod.Put, url, AdminSamples.UpdateBody(current, slug: "renamed"), token)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await env.SendAsync(HttpMethod.Put, url, AdminSamples.UpdateBody(current, variantActive: false), token)).Status);
    }

    [Fact]
    public async Task IA4_publish_and_unpublish_control_public_visibility_and_are_idempotent()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);
        string url = Url(created);
        Assert.Equal(HttpStatusCode.OK, (await env.SendAsync(HttpMethod.Put, url, AdminSamples.UpdateBody(created.Json, variantActive: true), token)).Status);

        IdentityTestEnv.Api published = await env.PostAsync(url + "/publish", null, token);
        Assert.Equal(HttpStatusCode.OK, published.Status);
        Assert.True(published.Json.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await env.GetAsync("/api/v1/products/linen-shirt")).Status);

        IdentityTestEnv.Api again = await env.PostAsync(url + "/publish", null, token);
        Assert.Equal(published.Json.GetProperty("version").GetUInt32(), again.Json.GetProperty("version").GetUInt32());

        Assert.Equal(HttpStatusCode.OK, (await env.PostAsync(url + "/unpublish", null, token)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync("/api/v1/products/linen-shirt")).Status);
    }

    [Fact]
    public async Task IA4_a_product_with_no_active_variant_cannot_be_published()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        object empty = new { slug = "empty", name = "Empty", description = "", categorySlug = "shirts", variants = Array.Empty<object>(), images = Array.Empty<object>() };
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token, empty);

        Assert.Equal(HttpStatusCode.Conflict, (await env.PostAsync(Url(created) + "/publish", null, token)).Status);
    }

    [Fact]
    public async Task IA5_preview_of_an_unpublished_product_has_the_public_shape_with_api_image_urls_and_is_not_visible()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api created = await AdminSamples.CreateProductAsync(env, token);
        string url = Url(created);
        IdentityTestEnv.Api withImage = await env.SendAsync(HttpMethod.Put, url,
            AdminSamples.UpdateBody(created.Json, variantActive: true, images: [new { storageKey = "dev/a.jpg", altText = "A" }]), token);
        Assert.Equal(HttpStatusCode.OK, withImage.Status);

        IdentityTestEnv.Api preview = await env.GetAsync(url + "/preview", token);

        Assert.Equal(HttpStatusCode.OK, preview.Status);
        Assert.False(preview.Json.GetProperty("isActive").GetBoolean());
        Assert.False(preview.Json.GetProperty("isVisibleToCustomers").GetBoolean());
        JsonElement product = preview.Json.GetProperty("product");
        Assert.Equal("linen-shirt", product.GetProperty("slug").GetString());
        Assert.Equal("/api/v1/images/dev/a.jpg", product.GetProperty("images")[0].GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await env.GetAsync("/api/v1/products/linen-shirt")).Status);
    }

    [Fact]
    public async Task IA6_list_filters_bounds_and_stable_order()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api a = await AdminSamples.CreateProductAsync(env, token, AdminSamples.Product("alpha", "A-1"));
        await AdminSamples.CreateProductAsync(env, token, AdminSamples.Product("beta", "B-1"));
        await env.SendAsync(HttpMethod.Put, Url(a), AdminSamples.UpdateBody(a.Json, variantActive: true), token);
        await env.PostAsync(Url(a) + "/publish", null, token);

        async Task<string[]> Slugs(string query)
        {
            IdentityTestEnv.Api r = await env.GetAsync(AdminSamples.Products + query, token);
            Assert.Equal(HttpStatusCode.OK, r.Status);
            return r.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()!).ToArray();
        }

        Assert.Equal(2, (await Slugs("")).Length);
        Assert.Equal(["alpha"], await Slugs("?status=active"));
        Assert.Equal(["beta"], await Slugs("?status=inactive"));
        Assert.Equal(["beta"], await Slugs("?search=beta"));
        Assert.Equal(2, (await Slugs("?category=shirts")).Length);
        Assert.Empty(await Slugs("?category=nothing"));
        Assert.Equal(await Slugs(""), await Slugs("?page=1&pageSize=20")); // stable, defaults are page 1 / size 20
        Assert.Single(await Slugs("?pageSize=1"));

        foreach (string? bad in new[] { "?status=bogus", "?page=0", "?pageSize=101", "?search=" + new string('s', 101) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await env.GetAsync(AdminSamples.Products + bad, token)).Status);
        }
    }

    [Fact]
    public async Task IA7_categories_can_be_created_listed_publicly_and_reject_duplicates_and_bad_parents()
    {
        (IdentityTestEnv? env, string? token) = await StartAsync(postgres);
        await using IdentityTestEnv _env = env;

        await AdminSamples.CreateCategoryAsync(env, token, "tops");
        await AdminSamples.CreateCategoryAsync(env, token, "tees", "tops");

        Assert.Equal(HttpStatusCode.Conflict, (await env.PostAsync(AdminSamples.Categories, new { slug = "tops", name = "Tops" }, token)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(AdminSamples.Categories, new { slug = "x", name = "X", parentSlug = "missing" }, token)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await env.PostAsync(AdminSamples.Categories, new { slug = "y", name = "Y", parentSlug = "tees" }, token)).Status);
        Assert.Contains("tees", (await env.GetAsync("/api/v1/categories")).Text);
    }
}
