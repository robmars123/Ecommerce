using Ecommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");
        builder.HasKey(i => i.Id).HasName("pk_product_images");
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.ProductId).HasColumnName("product_id");
        builder.Property(i => i.StorageKey).HasColumnName("storage_key").HasMaxLength(512).IsRequired();
        builder.Property(i => i.AltText).HasColumnName("alt_text").HasMaxLength(300).IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(i => new { i.ProductId, i.SortOrder }).HasDatabaseName("ix_product_images_product_id_sort_order");
    }
}
