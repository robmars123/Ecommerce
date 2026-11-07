using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Catalog.Domain;
using Ecommerce.Inventory.Contracts;
using Ecommerce.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Inventory;

/// <summary>Shared helpers for the Slice 2 tests: variants through the Catalog domain, stock through SQL, reserve through the real service.</summary>
internal static class InventoryEnv
{
    public static Task<IdentityTestEnv> StartAsync(
        PostgresFixture postgres, IReadOnlyDictionary<string, string>? settings = null, Action<IServiceCollection>? configure = null) =>
        IdentityTestEnv.StartAsync(postgres, settings: settings, configure: configure);

    public static string Cs(IdentityTestEnv env) => env.Db.Database.GetConnectionString()!;

    public static async Task<Guid> AddVariantAsync(IdentityTestEnv env, string sku, string productName = "Tee", bool productActive = true, bool variantActive = true) =>
        (await AddProductAsync(env, sku, productName, [sku], productActive, variantActive))[0];

    /// <summary>One product (slug "p-" + key, published unless told otherwise) with one variant per SKU; returns the variant IDs in SKU order.</summary>
    public static async Task<Guid[]> AddProductAsync(
        IdentityTestEnv env, string key, string productName, IReadOnlyList<string> skus, bool productActive = true, bool variantActive = true)
    {
        DateTimeOffset at = env.Clock.UtcNow;
        Category category = Category.Create(Guid.CreateVersion7(at), "cat-" + key.ToLowerInvariant(), "Cat " + key, null);
        Product product = Product.Create(Guid.CreateVersion7(at), "p-" + key.ToLowerInvariant(), productName, "", category.Id, at);
        List<ProductVariant> variants = skus
            .Select(sku => product.AddVariant(Guid.CreateVersion7(at), sku, sku, null, null, 10m, "USD", null, 100, at))
            .ToList();
        if (productActive)
        {
            product.Publish(at);
        }
        if (!variantActive)
        {
            variants.ForEach(v => v.Deactivate());
        }
        env.Db.Add(category);
        env.Db.Add(product);
        await env.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return variants.Select(v => v.Id).ToArray();
    }

    public static async Task SetStockAsync(IdentityTestEnv env, Guid variantId, int onHand, int reserved = 0)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(Cs(env));
        await using NpgsqlCommand cmd = source.CreateCommand(
            "INSERT INTO inventory_stock (product_variant_id, quantity_on_hand, quantity_reserved) VALUES (@id, @on, @res) " +
            "ON CONFLICT (product_variant_id) DO UPDATE SET quantity_on_hand = @on, quantity_reserved = @res");
        cmd.Parameters.AddWithValue("id", variantId);
        cmd.Parameters.AddWithValue("on", onHand);
        cmd.Parameters.AddWithValue("res", reserved);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Its own scope and its own transaction, like a checkout request.</summary>
    public static async Task<ReservationResult> ReserveAsync(
        IdentityTestEnv env, Guid orderId, IReadOnlyList<ReservationLine> lines, DateTimeOffset? expiresAt = null)
    {
        using IServiceScope scope = env.Factory.Services.CreateScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IInventoryReserver reserver = scope.ServiceProvider.GetRequiredService<IInventoryReserver>();
        DateTimeOffset expires = expiresAt ?? env.Clock.UtcNow.AddMinutes(15);
        return await unitOfWork.InTransactionAsync(ct => reserver.ReserveAsync(orderId, lines, expires, ct), TestContext.Current.CancellationToken);
    }

    public static async Task<(int OnHand, int Reserved)> ReadAsync(IdentityTestEnv env, Guid variantId)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(Cs(env));
        await using NpgsqlCommand cmd = source.CreateCommand("SELECT quantity_on_hand, quantity_reserved FROM inventory_stock WHERE product_variant_id = @id");
        cmd.Parameters.AddWithValue("id", variantId);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), "no stock row");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    public static async Task<long> CountAsync(IdentityTestEnv env, string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(Cs(env));
        await using NpgsqlCommand cmd = source.CreateCommand(sql);
        foreach ((string name, object value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        return (long)(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
