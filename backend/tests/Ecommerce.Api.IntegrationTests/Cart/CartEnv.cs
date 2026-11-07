using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Api.IntegrationTests.Inventory;
using Ecommerce.Identity.Domain;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Cart;

/// <summary>Two customers on a migrated database, plus variant and cart helpers. Fixture changes use parameterised SQL.</summary>
internal sealed class CartEnv : IAsyncDisposable
{
    public const string Key = "merge-key-0001";

    private CartEnv(IdentityTestEnv env, string a, string b, Guid aId)
    {
        Env = env;
        TokenA = a;
        TokenB = b;
        UserAId = aId;
    }

    public IdentityTestEnv Env { get; }
    public string TokenA { get; }
    public string TokenB { get; }
    public Guid UserAId { get; }

    public static async Task<CartEnv> StartAsync(
        PostgresFixture postgres, IReadOnlyDictionary<string, string>? settings = null, Action<IServiceCollection>? configure = null)
    {
        IdentityTestEnv env = await InventoryEnv.StartAsync(postgres, settings, configure);
        User a = await env.AddUserAsync("a@example.test");
        await env.AddUserAsync("b@example.test");
        return new CartEnv(env, await env.AccessTokenAsync("a@example.test"), await env.AccessTokenAsync("b@example.test"), a.Id);
    }

    public ValueTask DisposeAsync() => Env.DisposeAsync();

    /// <summary>A variant with stock on hand (default 20) priced 10.00 USD.</summary>
    public async Task<Guid> VariantAsync(string sku, int stock = 20)
    {
        Guid id = await InventoryEnv.AddVariantAsync(Env, sku);
        if (stock > 0)
        {
            await InventoryEnv.SetStockAsync(Env, id, stock);
        }
        return id;
    }

    public async Task ExecAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(InventoryEnv.Cs(Env));
        await using NpgsqlCommand cmd = source.CreateCommand(sql);
        foreach ((string name, object value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public Task SetPriceAsync(Guid variant, decimal price, string currency = "USD") =>
        ExecAsync("UPDATE product_variants SET price = @p, currency = @c WHERE id = @id", ("p", price), ("c", currency), ("id", variant));

    public Task DeactivateProductAsync(Guid variant) =>
        ExecAsync("UPDATE products SET is_active = false WHERE id = (SELECT product_id FROM product_variants WHERE id = @id)", ("id", variant));

    public Task DeactivateVariantAsync(Guid variant) =>
        ExecAsync("UPDATE product_variants SET is_active = false WHERE id = @id", ("id", variant));

    public Task<IdentityTestEnv.Api> GetAsync(string? token) => Env.GetAsync("/api/v1/cart", token);

    public Task<IdentityTestEnv.Api> AddAsync(string? token, Guid variant, int quantity) =>
        Env.PostAsync("/api/v1/cart/items", new { variantId = variant, quantity }, token);

    public Task<IdentityTestEnv.Api> SetAsync(string? token, Guid variant, int quantity) =>
        Env.SendAsync(HttpMethod.Put, $"/api/v1/cart/items/{variant}", new { quantity }, token);

    public Task<IdentityTestEnv.Api> DeleteAsync(string? token, Guid variant) =>
        Env.SendAsync(HttpMethod.Delete, $"/api/v1/cart/items/{variant}", null, token);

    public Task<IdentityTestEnv.Api> MergeAsync(string? token, string? key, params (Guid Variant, int Quantity)[] lines) =>
        MergeRawAsync(token, key, new { lines = lines.Select(l => new { variantId = l.Variant, quantity = l.Quantity }).ToArray() });

    public Task<IdentityTestEnv.Api> MergeRawAsync(string? token, string? key, object body) =>
        key is null
            ? Env.PostAsync("/api/v1/cart/merge", body, token)
            : Env.PostAsync("/api/v1/cart/merge", body, token, ("Idempotency-Key", key));

    /// <summary>The cart as a map variant to quantity, read through the API.</summary>
    public async Task<Dictionary<Guid, int>> QuantitiesAsync(string token)
    {
        IdentityTestEnv.Api r = await GetAsync(token);
        Assert.Equal(HttpStatusCode.OK, r.Status);
        return r.Json.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("variantId").GetGuid(), i => i.GetProperty("quantity").GetInt32());
    }

    public static JsonElement Line(IdentityTestEnv.Api cart, Guid variant) =>
        cart.Json.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("variantId").GetGuid() == variant);

    public static string[] Issues(JsonElement line) =>
        line.GetProperty("issues").EnumerateArray().Select(i => i.GetString()!).ToArray();
}
