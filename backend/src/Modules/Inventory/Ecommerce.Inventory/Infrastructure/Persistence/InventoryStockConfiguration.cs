using Ecommerce.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Inventory.Infrastructure.Persistence;

internal sealed class InventoryStockConfiguration : IEntityTypeConfiguration<InventoryStock>
{
    public void Configure(EntityTypeBuilder<InventoryStock> builder)
    {
        builder.ToTable("inventory_stock", t => t.HasCheckConstraint(
            "ck_inventory_stock_quantities", "quantity_on_hand - quantity_reserved >= 0 AND quantity_reserved >= 0"));
        builder.HasKey(s => s.ProductVariantId).HasName("pk_inventory_stock");
        builder.Property(s => s.ProductVariantId).HasColumnName("product_variant_id").ValueGeneratedNever();
        builder.Property(s => s.QuantityOnHand).HasColumnName("quantity_on_hand");
        builder.Property(s => s.QuantityReserved).HasColumnName("quantity_reserved");
        // xmin system column: needs no DDL, changes on every committed write (a second guard next to FOR UPDATE).
        builder.Property(s => s.Version).IsRowVersion();
    }
}
