using Ecommerce.Api.IntegrationTests.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Inventory;

public class InventorySchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_inventory_tables_constraints_and_indexes_exist()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);

        List<string> tables = await env.Db.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'").ToListAsync(ct);
        Assert.Contains("inventory_stock", tables);
        Assert.Contains("inventory_reservations", tables);

        List<string> checks = await env.Db.Database.SqlQueryRaw<string>(
            "SELECT conname AS \"Value\" FROM pg_constraint WHERE contype = 'c'").ToListAsync(ct);
        Assert.Contains("ck_inventory_stock_quantities", checks);
        Assert.Contains("ck_inventory_reservations_quantity", checks);
        Assert.Contains("ck_inventory_reservations_status", checks);

        List<string> indexes = await env.Db.Database.SqlQueryRaw<string>(
            "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync(ct);
        Assert.Contains(indexes, d => d.Contains("UNIQUE INDEX ux_inventory_reservations_order_variant_active", StringComparison.Ordinal)
            && d.Contains("(order_id, product_variant_id)", StringComparison.Ordinal) && d.Contains("'Active'", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("ix_inventory_reservations_expires_at_active", StringComparison.Ordinal)
            && d.Contains("(expires_at)", StringComparison.Ordinal) && d.Contains("'Active'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_database_refuses_reserved_above_on_hand_a_negative_reserved_and_a_bad_reservation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using IdentityTestEnv env = await InventoryEnv.StartAsync(postgres);
        Guid variant = Guid.NewGuid();
        await InventoryEnv.SetStockAsync(env, variant, 5, 2);

        PostgresException over = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using NpgsqlDataSource source = NpgsqlDataSource.Create(InventoryEnv.Cs(env));
            await using NpgsqlCommand cmd = source.CreateCommand("UPDATE inventory_stock SET quantity_reserved = quantity_on_hand + 1 WHERE product_variant_id = @id");
            cmd.Parameters.AddWithValue("id", variant);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Assert.Equal("23514", over.SqlState);
        Assert.Equal("ck_inventory_stock_quantities", over.ConstraintName);

        PostgresException negative = await Assert.ThrowsAsync<PostgresException>(() => InventoryEnv.SetStockAsync(env, variant, 5, -1));
        Assert.Equal("ck_inventory_stock_quantities", negative.ConstraintName);

        PostgresException badReservation = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using NpgsqlDataSource source = NpgsqlDataSource.Create(InventoryEnv.Cs(env));
            await using NpgsqlCommand cmd = source.CreateCommand(
                "INSERT INTO inventory_reservations (id, order_id, product_variant_id, quantity, status, expires_at, created_at) " +
                "VALUES (@id, @o, @v, 0, 'Active', now(), now())");
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("o", Guid.NewGuid());
            cmd.Parameters.AddWithValue("v", variant);
            await cmd.ExecuteNonQueryAsync(ct);
        });
        Assert.Equal("ck_inventory_reservations_quantity", badReservation.ConstraintName);
    }
}
