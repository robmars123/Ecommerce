using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Identity.Infrastructure.Persistence;

internal sealed class RefreshTokenFamilyConfiguration : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> builder)
    {
        builder.ToTable("refresh_token_families");
        builder.HasKey(f => f.Id).HasName("pk_refresh_token_families");
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.UserId).HasColumnName("user_id");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.AbsoluteExpiresAt).HasColumnName("absolute_expires_at");
        builder.Property(f => f.RevokedAt).HasColumnName("revoked_at");
        builder.Property(f => f.RevokedReason).HasColumnName("revoked_reason").HasConversion<string>().HasMaxLength(20);
        builder.Ignore(f => f.IsRevoked);

        builder.HasIndex(f => f.UserId).HasDatabaseName("ix_refresh_token_families_user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .HasConstraintName("fk_refresh_token_families_user_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
