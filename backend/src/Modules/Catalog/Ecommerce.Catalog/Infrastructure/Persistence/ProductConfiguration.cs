using Ecommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <summary>Name of the shadow property that maps the generated tsvector column (keeps Domain free of Npgsql).</summary>
    public const string SearchVector = "SearchVector";

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id).HasName("pk_products");
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(Product.MaxDescriptionLength).IsRequired();
        builder.Property(p => p.CategoryId).HasColumnName("category_id");
        builder.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(false);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.Property<NpgsqlTsVector>(SearchVector)
            .HasColumnName("search_vector")
            .HasComputedColumnSql(
                "setweight(to_tsvector('english', coalesce(name, '')), 'A') || " +
                "setweight(to_tsvector('english', coalesce(description, '')), 'B')",
                stored: true);

        builder.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("ux_products_slug");
        builder.HasIndex(SearchVector).HasMethod("GIN").HasDatabaseName("ix_products_search_vector");
        builder.HasIndex(p => new { p.CategoryId, p.IsActive }).HasDatabaseName("ix_products_category_id_is_active");
        builder.HasIndex(p => new { p.CreatedAt, p.Id })
            .IsDescending(true, true)
            .HasFilter("is_active")
            .HasDatabaseName("ix_products_active_newest");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .HasConstraintName("fk_products_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Variants)
            .WithOne()
            .HasForeignKey(v => v.ProductId)
            .HasConstraintName("fk_product_variants_product_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .HasConstraintName("fk_product_images_product_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
