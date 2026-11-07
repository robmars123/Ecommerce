using Npgsql;
using NpgsqlTypes;

namespace Ecommerce.Api.IntegrationTests.Orders;

internal sealed record SeedItem(string Sku, string Name, decimal UnitPrice, int Quantity);

internal sealed record SeedHistory(string? From, string To, DateTimeOffset At, string? Reason = null, Guid? ChangedBy = null);

internal sealed record SeedShipment(string Carrier, string TrackingNumber, DateTimeOffset ShippedAt, DateTimeOffset? DeliveredAt = null);

/// <summary>
/// Test-only order fixture (Slice 5 D3). Slice 4 adds checkout, but tests that need an order in a given state still insert
/// rows with parameterised SQL. This also exercises the real columns and CHECK constraints. Never moves money or stock.
/// </summary>
internal static class OrderSeed
{
    public const decimal ShippingTotal = 5.00m;

    public static async Task<Guid> InsertAsync(
        string connectionString,
        Guid userId,
        string status,
        DateTimeOffset createdAt,
        IReadOnlyList<SeedItem>? items = null,
        IReadOnlyList<SeedHistory>? history = null,
        SeedShipment? shipment = null,
        string? orderNumber = null)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid orderId = Guid.NewGuid();
        IReadOnlyList<SeedItem> lines = items ?? [new SeedItem("TEE-BLK-M", "Black tee, M", 20.00m, 1)];
        decimal subtotal = lines.Sum(i => i.UnitPrice * i.Quantity);

        await using NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection connection = await source.OpenConnectionAsync(ct);

        await using (NpgsqlCommand cmd = new NpgsqlCommand(
            "INSERT INTO orders (id, order_number, user_id, status, subtotal, discount_total, shipping_total, tax_total, grand_total, currency, " +
            "shipping_method_code, shipping_method_name, shipping_address_snapshot, version, created_at, updated_at, idempotency_key) " +
            "VALUES (@id, @number, @user, @status, @subtotal, 0, @shipping, 0, @grand, 'USD', 'standard', 'Standard', @address, 0, @at, @at, @key)",
            connection))
        {
            cmd.Parameters.AddWithValue("id", orderId);
            cmd.Parameters.AddWithValue("number", orderNumber ?? $"ORD-2026-{Random.Shared.Next(100000, 999999)}");
            cmd.Parameters.AddWithValue("user", userId);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("subtotal", subtotal);
            cmd.Parameters.AddWithValue("shipping", ShippingTotal);
            cmd.Parameters.AddWithValue("grand", subtotal + ShippingTotal);
            cmd.Parameters.Add(new NpgsqlParameter("address", NpgsqlDbType.Jsonb)
            {
                Value = "{\"fullName\":\"Test Buyer\",\"line1\":\"1 Example Street\",\"line2\":null,\"city\":\"Exampleville\",\"region\":null,\"postalCode\":\"12345\",\"country\":\"US\"}",
            });
            cmd.Parameters.AddWithValue("at", createdAt);
            cmd.Parameters.AddWithValue("key", "seed-" + orderId.ToString("N"));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        foreach (SeedItem item in lines)
        {
            await using NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO order_items (id, order_id, product_variant_id, sku_snapshot, name_snapshot, unit_price_snapshot, quantity, line_total) " +
                "VALUES (@id, @order, @variant, @sku, @name, @price, @qty, @total)", connection);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("order", orderId);
            cmd.Parameters.AddWithValue("variant", Guid.NewGuid());
            cmd.Parameters.AddWithValue("sku", item.Sku);
            cmd.Parameters.AddWithValue("name", item.Name);
            cmd.Parameters.AddWithValue("price", item.UnitPrice);
            cmd.Parameters.AddWithValue("qty", item.Quantity);
            cmd.Parameters.AddWithValue("total", item.UnitPrice * item.Quantity);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        foreach (SeedHistory row in history ?? [new SeedHistory(null, status, createdAt)])
        {
            await using NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO order_status_history (id, order_id, from_status, to_status, changed_by, reason, created_at) " +
                "VALUES (@id, @order, @from, @to, @by, @reason, @at)", connection);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("order", orderId);
            cmd.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.Text) { Value = (object?)row.From ?? DBNull.Value });
            cmd.Parameters.AddWithValue("to", row.To);
            cmd.Parameters.Add(new NpgsqlParameter("by", NpgsqlDbType.Uuid) { Value = (object?)row.ChangedBy ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("reason", NpgsqlDbType.Text) { Value = (object?)row.Reason ?? DBNull.Value });
            cmd.Parameters.AddWithValue("at", row.At);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        if (shipment is not null)
        {
            await using NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO shipments (id, order_id, carrier, tracking_number, shipped_at, delivered_at) VALUES (@id, @order, @carrier, @tracking, @shipped, @delivered)",
                connection);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("order", orderId);
            cmd.Parameters.AddWithValue("carrier", shipment.Carrier);
            cmd.Parameters.AddWithValue("tracking", shipment.TrackingNumber);
            cmd.Parameters.AddWithValue("shipped", shipment.ShippedAt);
            cmd.Parameters.Add(new NpgsqlParameter("delivered", NpgsqlDbType.TimestampTz) { Value = (object?)shipment.DeliveredAt ?? DBNull.Value });
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return orderId;
    }

    public static async Task<long> CountAsync(string connectionString, string sql, Guid orderId)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlCommand cmd = source.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", orderId);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
