using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Identity.Infrastructure.Persistence;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id).HasName("pk_refresh_tokens");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.FamilyId).HasColumnName("family_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasColumnType("bytea").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(t => t.ReplacedById).HasColumnName("replaced_by_id");

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(t => t.FamilyId).HasDatabaseName("ix_refresh_tokens_family_id");
        builder.HasOne<RefreshTokenFamily>()
            .WithMany()
            .HasForeignKey(t => t.FamilyId)
            .HasConstraintName("fk_refresh_tokens_family_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
