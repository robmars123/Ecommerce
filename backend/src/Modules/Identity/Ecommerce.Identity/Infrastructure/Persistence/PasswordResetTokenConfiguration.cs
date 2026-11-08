using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Identity.Infrastructure.Persistence;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(t => t.Id).HasName("pk_password_reset_tokens");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasColumnType("bytea").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.UsedAt).HasColumnName("used_at");
        builder.Property(t => t.InvalidatedAt).HasColumnName("invalidated_at");
        builder.Property(t => t.RequestedAt).HasColumnName("requested_at");

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_password_reset_tokens_token_hash");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_password_reset_tokens_user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .HasConstraintName("fk_password_reset_tokens_user_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
