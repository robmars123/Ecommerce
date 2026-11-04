using Ecommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id).HasName("pk_categories");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(Category.MaxNameLength).IsRequired();
        builder.Property(c => c.ParentId).HasColumnName("parent_id");

        builder.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("ux_categories_slug");
        builder.HasIndex(c => c.ParentId).HasDatabaseName("ix_categories_parent_id");
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .HasConstraintName("fk_categories_parent_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
