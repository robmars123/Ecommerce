using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Cart.Infrastructure.Persistence;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Domain.Cart>
{
    public void Configure(EntityTypeBuilder<Domain.Cart> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(c => c.Id).HasName("pk_carts");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(c => c.UserId).IsUnique().HasDatabaseName("ux_carts_user_id");
        builder.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CartId)
            .HasConstraintName("fk_cart_items_carts").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
