using Ecommerce.Cart.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Cart.Infrastructure.Persistence;

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items", t => t.HasCheckConstraint("ck_cart_items_quantity", "quantity > 0"));
        builder.HasKey(i => i.Id).HasName("pk_cart_items");
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.CartId).HasColumnName("cart_id");
        builder.Property(i => i.ProductVariantId).HasColumnName("product_variant_id");
        builder.Property(i => i.Quantity).HasColumnName("quantity");
        builder.Property(i => i.DisplayUnitPrice).HasColumnName("display_unit_price").HasColumnType("numeric(19,4)");
        builder.Property(i => i.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(i => i.AddedAt).HasColumnName("added_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(i => new { i.CartId, i.ProductVariantId }).IsUnique().HasDatabaseName("ux_cart_items_cart_variant");
    }
}
