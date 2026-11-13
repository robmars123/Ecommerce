using Ecommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

internal sealed class ImageBlobConfiguration : IEntityTypeConfiguration<ImageBlob>
{
    public void Configure(EntityTypeBuilder<ImageBlob> builder)
    {
        builder.ToTable("image_blobs");
        builder.HasKey(b => b.StorageKey).HasName("pk_image_blobs");
        builder.Property(b => b.StorageKey).HasColumnName("storage_key").HasMaxLength(512).ValueGeneratedNever();
        builder.Property(b => b.ContentType).HasColumnName("content_type").HasMaxLength(32).IsRequired();
        builder.Property(b => b.SizeBytes).HasColumnName("size_bytes");
        builder.Property(b => b.Sha256).HasColumnName("sha256").IsRequired();
        builder.Property(b => b.Content).HasColumnName("content").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
    }
}
