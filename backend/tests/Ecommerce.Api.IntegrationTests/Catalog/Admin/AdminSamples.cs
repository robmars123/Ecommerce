using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;

namespace Ecommerce.Api.IntegrationTests.Catalog.Admin;

/// <summary>Request bodies and a signed-in admin for the admin catalog tests.</summary>
internal static class AdminSamples
{
    public const string Products = "/api/v1/admin/products";
    public const string Categories = "/api/v1/admin/categories";

    public static object Variant(string sku, string amount = "49.00", object? compareAt = null, int weight = 250) => new
    {
        sku,
        name = "Default",
        size = (string?)null,
        colour = (string?)null,
        price = new { amount, currency = "USD" },
        compareAtPrice = compareAt,
        weightGrams = weight,
    };

    public static object Product(string slug = "linen-shirt", string sku = "LS-1", string category = "shirts", object[]? variants = null) => new
    {
        slug,
        name = "Linen " + slug,
        description = "A shirt.",
        categorySlug = category,
        variants = variants ?? [Variant(sku)],
        images = Array.Empty<object>(),
    };

    public static async Task<string> AdminTokenAsync(IdentityTestEnv env, string email = "admin@example.test")
    {
        await env.AddUserAsync(email, UserRole.Admin);
        return await env.AccessTokenAsync(email);
    }

    public static async Task<IdentityTestEnv.Api> CreateCategoryAsync(IdentityTestEnv env, string token, string slug = "shirts", string? parent = null)
    {
        IdentityTestEnv.Api r = await env.PostAsync(Categories, new { slug, name = slug, parentSlug = parent }, token);
        Assert.Equal(HttpStatusCode.Created, r.Status);
        return r;
    }

    /// <summary>Creates category "shirts" if needed and a product; returns the created product JSON.</summary>
    public static async Task<IdentityTestEnv.Api> CreateProductAsync(IdentityTestEnv env, string token, object? body = null)
    {
        await env.PostAsync(Categories, new { slug = "shirts", name = "shirts", parentSlug = (string?)null }, token);
        IdentityTestEnv.Api r = await env.PostAsync(Products, body ?? Product(), token);
        Assert.Equal(HttpStatusCode.Created, r.Status);
        return r;
    }

    /// <summary>A PUT body built from a created product, so tests change only what they mean to.</summary>
    public static object UpdateBody(System.Text.Json.JsonElement product, string? name = null, string? slug = null, bool? variantActive = null, string? currency = null, object[]? images = null)
    {
        JsonElement v = product.GetProperty("variants")[0];
        return new
        {
            version = product.GetProperty("version").GetUInt32(),
            slug = slug ?? product.GetProperty("slug").GetString(),
            name = name ?? product.GetProperty("name").GetString(),
            description = product.GetProperty("description").GetString(),
            categorySlug = product.GetProperty("category").GetProperty("slug").GetString(),
            variants = new[]
            {
                new
                {
                    id = (Guid?)v.GetProperty("id").GetGuid(),
                    sku = v.GetProperty("sku").GetString(),
                    name = v.GetProperty("name").GetString(),
                    size = (string?)null,
                    colour = (string?)null,
                    price = new { amount = v.GetProperty("price").GetProperty("amount").GetString(), currency = currency ?? "USD" },
                    compareAtPrice = (object?)null,
                    weightGrams = v.GetProperty("weightGrams").GetInt32(),
                    isActive = variantActive ?? v.GetProperty("isActive").GetBoolean(),
                },
            },
            images = images ?? [],
        };
    }
}
