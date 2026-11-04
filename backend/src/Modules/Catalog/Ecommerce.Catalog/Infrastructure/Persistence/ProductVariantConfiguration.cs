using Ecommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants", t =>
        {
            t.HasCheckConstraint("ck_product_variants_price_nonneg", "price >= 0");
            t.HasCheckConstraint("ck_product_variants_compare_at", "compare_at_price IS NULL OR compare_at_price > price");
            t.HasCheckConstraint("ck_product_variants_currency", "currency ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint("ck_product_variants_weight", "weight_grams >= 0");
        });
        builder.HasKey(v => v.Id).HasName("pk_product_variants");
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(v => v.ProductId).HasColumnName("product_id");
        builder.Property(v => v.Sku).HasColumnName("sku").HasMaxLength(Product.MaxSkuLength).IsRequired();
        builder.Property(v => v.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(v => v.Size).HasColumnName("size").HasMaxLength(50);
        builder.Property(v => v.Colour).HasColumnName("colour").HasMaxLength(50);
        builder.Property(v => v.Price).HasColumnName("price").HasColumnType("numeric(19,4)");
        builder.Property(v => v.Currency).HasColumnName("currency").HasColumnType("character(3)").IsRequired();
        builder.Property(v => v.CompareAtPrice).HasColumnName("compare_at_price").HasColumnType("numeric(19,4)");
        builder.Property(v => v.WeightGrams).HasColumnName("weight_grams");
        builder.Property(v => v.IsActive).HasColumnName("is_active");

        builder.HasIndex(v => v.Sku).IsUnique().HasDatabaseName("ux_product_variants_sku");
        builder.HasIndex(v => v.ProductId).HasDatabaseName("ix_product_variants_product_id");
    }
}
