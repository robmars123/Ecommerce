using System.Text.Json;
using Ecommerce.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Orders.Infrastructure.Persistence;

internal static class OrderStatusSql
{
    public const string Values = "'PendingPayment','Paid','Processing','Shipped','Delivered','Cancelled','Refunded','Returned'";
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t =>
        {
            t.HasCheckConstraint("ck_orders_status", $"status IN ({OrderStatusSql.Values})");
            t.HasCheckConstraint("ck_orders_totals_nonneg",
                "subtotal >= 0 AND discount_total >= 0 AND shipping_total >= 0 AND tax_total >= 0 AND grand_total >= 0");
            t.HasCheckConstraint("ck_orders_currency", "currency ~ '^[A-Z]{3}$'");
        });
        builder.HasKey(o => o.Id).HasName("pk_orders");
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.OrderNumber).HasColumnName("order_number").HasMaxLength(40).IsRequired();
        builder.Property(o => o.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(19,4)");
        builder.Property(o => o.DiscountTotal).HasColumnName("discount_total").HasColumnType("numeric(19,4)");
        builder.Property(o => o.ShippingTotal).HasColumnName("shipping_total").HasColumnType("numeric(19,4)");
        builder.Property(o => o.TaxTotal).HasColumnName("tax_total").HasColumnType("numeric(19,4)");
        builder.Property(o => o.GrandTotal).HasColumnName("grand_total").HasColumnType("numeric(19,4)");
        builder.Property(o => o.Currency).HasColumnName("currency").HasColumnType("character(3)").IsRequired();
        builder.Property(o => o.ShippingMethodCode).HasColumnName("shipping_method_code").HasMaxLength(50).IsRequired();
        builder.Property(o => o.ShippingMethodName).HasColumnName("shipping_method_name").HasMaxLength(100).IsRequired();
        builder.Property(o => o.ShippingAddress).HasColumnName("shipping_address_snapshot").HasColumnType("jsonb")
            .HasConversion(
                a => JsonSerializer.Serialize(a, AddressJson.Options),
                j => JsonSerializer.Deserialize<ShippingAddress>(j, AddressJson.Options)!)
            .IsRequired();
        // D7: a plain int the domain bumps on every transition; the UPDATE carries WHERE version = original.
        builder.Property(o => o.Version).HasColumnName("version").HasDefaultValue(0).IsConcurrencyToken();
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(o => o.OrderNumber).IsUnique().HasDatabaseName("ux_orders_order_number");
        builder.HasIndex(o => o.IdempotencyKey).IsUnique().HasDatabaseName("ux_orders_idempotency_key");
        builder.HasIndex(o => new { o.UserId, o.CreatedAt }).IsDescending(false, true).HasDatabaseName("ix_orders_user_id_created_at");
        builder.HasIndex(o => new { o.Status, o.CreatedAt }).IsDescending(false, true).HasDatabaseName("ix_orders_status_created_at");

        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId)
            .HasConstraintName("fk_order_items_order_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.History).WithOne().HasForeignKey(h => h.OrderId)
            .HasConstraintName("fk_order_status_history_order_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Shipments).WithOne().HasForeignKey(s => s.OrderId)
            .HasConstraintName("fk_shipments_order_id").OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.Shipments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal static class AddressJson
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("ck_order_items_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_order_items_amounts_nonneg", "unit_price_snapshot >= 0 AND line_total >= 0");
        });
        builder.HasKey(i => i.Id).HasName("pk_order_items");
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.OrderId).HasColumnName("order_id");
        builder.Property(i => i.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(i => i.SkuSnapshot).HasColumnName("sku_snapshot").HasMaxLength(64).IsRequired();
        builder.Property(i => i.NameSnapshot).HasColumnName("name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(i => i.UnitPriceSnapshot).HasColumnName("unit_price_snapshot").HasColumnType("numeric(19,4)");
        builder.Property(i => i.Quantity).HasColumnName("quantity");
        builder.Property(i => i.LineTotal).HasColumnName("line_total").HasColumnType("numeric(19,4)");
        builder.HasIndex(i => i.OrderId).HasDatabaseName("ix_order_items_order_id");
    }
}

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.ToTable("order_status_history", t =>
        {
            t.HasCheckConstraint("ck_order_status_history_from", $"from_status IS NULL OR from_status IN ({OrderStatusSql.Values})");
            t.HasCheckConstraint("ck_order_status_history_to", $"to_status IN ({OrderStatusSql.Values})");
        });
        builder.HasKey(h => h.Id).HasName("pk_order_status_history");
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(h => h.OrderId).HasColumnName("order_id");
        builder.Property(h => h.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.ChangedBy).HasColumnName("changed_by");
        builder.Property(h => h.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(h => h.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(h => new { h.OrderId, h.CreatedAt }).HasDatabaseName("ix_order_status_history_order_id_created_at");
    }
}

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments");
        builder.HasKey(s => s.Id).HasName("pk_shipments");
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.OrderId).HasColumnName("order_id");
        builder.Property(s => s.Carrier).HasColumnName("carrier").HasMaxLength(50).IsRequired();
        builder.Property(s => s.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(64).IsRequired();
        builder.Property(s => s.ShippedAt).HasColumnName("shipped_at");
        builder.Property(s => s.DeliveredAt).HasColumnName("delivered_at");
        builder.HasIndex(s => s.OrderId).HasDatabaseName("ix_shipments_order_id");
    }
}
