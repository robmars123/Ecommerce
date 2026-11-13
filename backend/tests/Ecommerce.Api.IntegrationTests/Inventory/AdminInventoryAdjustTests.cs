using System.Net;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;
using Ecommerce.Inventory.Contracts;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Inventory;

// Test 10 and the adjust endpoint's auth, validation and concurrency rules.
public class AdminInventoryAdjustTests(PostgresFixture postgres)
{
    private const string AdjustUrl = "/api/v1/admin/inventory/adjust";

    private sealed record AuditRow(Guid? ActorId, string Action, string? Before, string? After, string? Reason);

    private sealed record Fixture(IdentityTestEnv Env, string Admin, string Customer, Guid AdminId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Env.DisposeAsync();

        public Task<IdentityTestEnv.Api> AdjustAsync(Guid variant, int delta, int expectedOnHand, string reason = "Cycle count", string? token = null) =>
            Env.PostAsync(AdjustUrl, new { variantId = variant, delta, expectedOnHand, reason }, token ?? Admin);
    }

    private async Task<Fixture> StartAsync()
    {
        IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        User admin = await env.AddUserAsync("admin@example.test", UserRole.Admin);
        await env.AddUserAsync("customer@example.test");
        return new Fixture(env, await env.AccessTokenAsync("admin@example.test"), await env.AccessTokenAsync("customer@example.test"), admin.Id);
    }

    private static async Task<List<AuditRow>> AuditAsync(IdentityTestEnv env, Guid variant)
    {
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(InventoryEnv.Cs(env));
        await using NpgsqlCommand cmd = source.CreateCommand(
            "SELECT actor_id, action, before_json::text, after_json::text, reason FROM audit_log WHERE entity_id = @id ORDER BY created_at, id");
        cmd.Parameters.AddWithValue("id", variant);
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        List<AuditRow> rows = [];
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new AuditRow(
                reader.IsDBNull(0) ? null : reader.GetGuid(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        return rows;
    }

    private static int Int(string json, string name) => JsonDocument.Parse(json).RootElement.GetProperty(name).GetInt32();

    [Fact]
    public async Task T10_an_adjust_that_would_drop_available_below_zero_is_409_leaves_stock_alone_and_writes_one_rejected_audit_row()
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "T10-1");
        await InventoryEnv.SetStockAsync(f.Env, variant, 5);
        Assert.True((await InventoryEnv.ReserveAsync(f.Env, Guid.NewGuid(), [new ReservationLine(variant, 3)])).Succeeded);

        IdentityTestEnv.Api r = await f.AdjustAsync(variant, -3, 5, "Damaged in transit");

        Assert.Equal(HttpStatusCode.Conflict, r.Status);
        Assert.Equal(2, r.Json.GetProperty("available").GetInt32());
        Assert.Equal(5, r.Json.GetProperty("onHand").GetInt32());
        Assert.Equal(3, r.Json.GetProperty("reserved").GetInt32());
        Assert.Equal((5, 3), await InventoryEnv.ReadAsync(f.Env, variant));
        AuditRow row = Assert.Single(await AuditAsync(f.Env, variant));
        Assert.Equal("inventory.adjust.rejected", row.Action);
        Assert.Equal(f.AdminId, row.ActorId);
        Assert.Equal("Damaged in transit", row.Reason);
        Assert.Equal(5, Int(row.Before!, "onHand"));
        Assert.Equal(3, Int(row.Before!, "reserved"));
        Assert.Equal(-3, Int(row.After!, "attemptedDelta"));
    }

    [Fact]
    public async Task A_successful_adjust_is_200_with_the_item_and_one_audit_row_with_before_and_after()
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "OK-1", "Linen shirt");
        await InventoryEnv.SetStockAsync(f.Env, variant, 5);

        IdentityTestEnv.Api r = await f.AdjustAsync(variant, 2, 5, "Restock");

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal(variant, r.Json.GetProperty("variantId").GetGuid());
        Assert.Equal("Linen shirt", r.Str("productName"));
        Assert.Equal("OK-1", r.Str("sku"));
        Assert.Equal(7, r.Json.GetProperty("onHand").GetInt32());
        Assert.Equal(0, r.Json.GetProperty("reserved").GetInt32());
        Assert.Equal(7, r.Json.GetProperty("available").GetInt32());
        Assert.Equal("InStock", r.Str("stockStatus"));
        Assert.True(r.Json.GetProperty("isActive").GetBoolean());
        Assert.Equal((7, 0), await InventoryEnv.ReadAsync(f.Env, variant));
        AuditRow row = Assert.Single(await AuditAsync(f.Env, variant));
        Assert.Equal("inventory.adjust", row.Action);
        Assert.Equal(f.AdminId, row.ActorId);
        Assert.Equal("Restock", row.Reason);
        Assert.Equal(5, Int(row.Before!, "onHand"));
        Assert.Equal(7, Int(row.After!, "onHand"));
    }

    [Fact]
    public async Task A_stale_expectedOnHand_is_409_changes_nothing_and_writes_no_audit_row_so_a_double_submit_applies_once()
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "STALE-1");
        await InventoryEnv.SetStockAsync(f.Env, variant, 5);

        Assert.Equal(HttpStatusCode.OK, (await f.AdjustAsync(variant, 3, 5)).Status);
        IdentityTestEnv.Api again = await f.AdjustAsync(variant, 3, 5);

        Assert.Equal(HttpStatusCode.Conflict, again.Status);
        Assert.Equal((8, 0), await InventoryEnv.ReadAsync(f.Env, variant));
        Assert.Equal("inventory.adjust", Assert.Single(await AuditAsync(f.Env, variant)).Action);
    }

    [Fact]
    public async Task The_first_adjust_on_a_variant_with_no_stock_row_creates_it()
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "NEW-1");

        IdentityTestEnv.Api r = await f.AdjustAsync(variant, 10, 0, "Initial count");

        Assert.Equal(HttpStatusCode.OK, r.Status);
        Assert.Equal((10, 0), await InventoryEnv.ReadAsync(f.Env, variant));
    }

    [Fact]
    public async Task An_unknown_variant_is_404_and_writes_nothing()
    {
        await using Fixture f = await StartAsync();
        Guid unknown = Guid.NewGuid();

        IdentityTestEnv.Api r = await f.AdjustAsync(unknown, 1, 0);

        Assert.Equal(HttpStatusCode.NotFound, r.Status);
        Assert.Equal(0, await InventoryEnv.CountAsync(f.Env, "SELECT count(*) FROM inventory_stock"));
        Assert.Empty(await AuditAsync(f.Env, unknown));
    }

    [Fact]
    public async Task Both_endpoints_are_401_for_anonymous_and_403_for_a_customer()
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "AUTH-1");

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.GetAsync("/api/v1/admin/inventory")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Env.PostAsync(AdjustUrl, new { variantId = variant, delta = 1, expectedOnHand = 0, reason = "x" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Env.GetAsync("/api/v1/admin/inventory", f.Customer)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.AdjustAsync(variant, 1, 0, "x", f.Customer)).Status);
        Assert.Equal(0, await InventoryEnv.CountAsync(f.Env, "SELECT count(*) FROM inventory_stock"));
    }

    [Fact]
    public async Task Both_endpoints_are_403_for_support_and_write_nothing()
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "AUTH-2");
        await f.Env.AddUserAsync("support@example.test", UserRole.Support);
        string support = await f.Env.AccessTokenAsync("support@example.test");

        Assert.Equal(HttpStatusCode.Forbidden, (await f.Env.GetAsync("/api/v1/admin/inventory", support)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.AdjustAsync(variant, 1, 0, "x", support)).Status);
        Assert.Equal(0, await InventoryEnv.CountAsync(f.Env, "SELECT count(*) FROM inventory_stock"));
    }

    [Theory]
    [InlineData(0, 0, "Reason")]
    [InlineData(1_000_001, 0, "Reason")]
    [InlineData(-1_000_001, 0, "Reason")]
    [InlineData(1, -1, "Reason")]
    [InlineData(1, 0, "")]
    [InlineData(1, 0, "   ")]
    public async Task An_invalid_adjust_is_400_and_writes_nothing(int delta, int expected, string reason)
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "BAD-1");

        IdentityTestEnv.Api r = await f.AdjustAsync(variant, delta, expected, reason);

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.Equal(0, await InventoryEnv.CountAsync(f.Env, "SELECT count(*) FROM inventory_stock"));
        Assert.Empty(await AuditAsync(f.Env, variant));
    }

    [Fact]
    public async Task A_501_character_reason_a_missing_field_and_an_unknown_field_are_400()
    {
        await using Fixture f = await StartAsync();
        Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "BAD-2");

        Assert.Equal(HttpStatusCode.BadRequest, (await f.AdjustAsync(variant, 1, 0, new string('x', 501))).Status);
        Assert.Equal(HttpStatusCode.OK, (await f.AdjustAsync(variant, 1, 0, new string('x', 500))).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Env.PostAsync(AdjustUrl, new { variantId = variant, delta = 1, reason = "x" }, f.Admin)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Env.PostAsync(AdjustUrl, new { variantId = variant, delta = 1, expectedOnHand = 1, reason = "x", onHand = 99 }, f.Admin)).Status);
    }

    [Fact]
    public async Task A_concurrent_adjust_and_reserve_on_one_row_never_drive_available_below_zero_and_exactly_one_wins()
    {
        await using Fixture f = await StartAsync();
        for (int round = 0; round < 5; round++)
        {
            Guid variant = await InventoryEnv.AddVariantAsync(f.Env, "RACE-" + round);
            await InventoryEnv.SetStockAsync(f.Env, variant, 5);
            TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<IdentityTestEnv.Api> adjust = Task.Run(async () =>
            {
                await gate.Task;
                return await f.AdjustAsync(variant, -5, 5, "Write-off");
            });
            Task<ReservationResult> reserve = Task.Run(async () =>
            {
                await gate.Task;
                return await InventoryEnv.ReserveAsync(f.Env, Guid.NewGuid(), [new ReservationLine(variant, 3)]);
            });
            gate.SetResult();
            await Task.WhenAll(adjust, reserve).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            bool adjusted = (await adjust).Status == HttpStatusCode.OK;
            Assert.Equal(adjusted ? HttpStatusCode.OK : HttpStatusCode.Conflict, (await adjust).Status);
            Assert.NotEqual(adjusted, (await reserve).Succeeded);
            (int onHand, int reserved) = await InventoryEnv.ReadAsync(f.Env, variant);
            Assert.True(onHand - reserved >= 0);
            Assert.Equal(adjusted ? (0, 0) : (5, 3), (onHand, reserved));
        }
    }
}
