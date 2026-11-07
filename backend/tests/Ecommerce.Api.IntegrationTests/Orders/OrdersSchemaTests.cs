using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecommerce.Api.IntegrationTests.Orders;

public class OrdersSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_orders_tables_and_day_one_indexes_exist()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);

        List<string> tables = await env.Db.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'").ToListAsync(ct);
        foreach (string table in new[] { "orders", "order_items", "order_status_history", "shipments", "audit_log" })
        {
            Assert.Contains(table, tables);
        }

        List<string> indexes = await env.Db.Database.SqlQueryRaw<string>(
            "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'").ToListAsync(ct);
        Assert.Contains(indexes, d => d.Contains("UNIQUE INDEX ux_orders_order_number", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("ix_orders_user_id_created_at", StringComparison.Ordinal) && d.Contains("created_at DESC", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("ix_orders_status_created_at", StringComparison.Ordinal) && d.Contains("created_at DESC", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("ix_order_items_order_id", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("ix_order_status_history_order_id_created_at", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("ix_shipments_order_id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_database_rejects_an_unknown_order_status_a_zero_quantity_and_a_history_status_outside_the_list()
    {
        await using IdentityTestEnv env = await OrdersEnv.StartAsync(postgres);
        User user = await env.AddUserAsync("a@example.test");
        string cs = OrdersEnv.ConnectionString(env);

        PostgresException badStatus = await Assert.ThrowsAsync<PostgresException>(
            () => OrderSeed.InsertAsync(cs, user.Id, "Teleported", env.Clock.UtcNow));
        Assert.Equal("ck_orders_status", badStatus.ConstraintName);

        PostgresException badQuantity = await Assert.ThrowsAsync<PostgresException>(
            () => OrderSeed.InsertAsync(cs, user.Id, "Paid", env.Clock.UtcNow, items: [new SeedItem("X-1", "X", 1m, 0)]));
        Assert.Equal("ck_order_items_quantity", badQuantity.ConstraintName);

        PostgresException badHistory = await Assert.ThrowsAsync<PostgresException>(
            () => OrderSeed.InsertAsync(cs, user.Id, "Paid", env.Clock.UtcNow, history: [new SeedHistory(null, "Teleported", env.Clock.UtcNow)]));
        Assert.Equal("ck_order_status_history_to", badHistory.ConstraintName);
    }
}
