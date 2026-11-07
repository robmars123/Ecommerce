using Ecommerce.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Inventory.Infrastructure.Persistence;

internal sealed class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        builder.ToTable("inventory_reservations", t =>
        {
            t.HasCheckConstraint("ck_inventory_reservations_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_inventory_reservations_status", "status IN ('Active','Committed','Released','Expired')");
        });
        builder.HasKey(r => r.Id).HasName("pk_inventory_reservations");
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.OrderId).HasColumnName("order_id");
        builder.Property(r => r.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(r => r.Quantity).HasColumnName("quantity");
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        // Same module, so a real FK. No FK to orders (another module).
        builder.HasOne<InventoryStock>().WithMany()
            .HasForeignKey(r => r.ProductVariantId)
            .HasConstraintName("fk_inventory_reservations_stock")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.OrderId, r.ProductVariantId })
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ux_inventory_reservations_order_variant_active");
        builder.HasIndex(r => r.ProductVariantId).HasDatabaseName("ix_inventory_reservations_product_variant_id");
        builder.HasIndex(r => r.ExpiresAt)
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ix_inventory_reservations_expires_at_active");
    }
}
