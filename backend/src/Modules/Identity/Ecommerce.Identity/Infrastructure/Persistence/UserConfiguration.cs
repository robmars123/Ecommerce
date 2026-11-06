using Ecommerce.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Identity.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_role", "role IN ('Customer','Support','Admin')");
            t.HasCheckConstraint("ck_users_access_failed_count", "access_failed_count >= 0");
        });
        builder.HasKey(u => u.Id).HasName("pk_users");
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.Email).HasColumnName("email").HasColumnType("citext").IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(User.MaxDisplayNameLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(64).IsRequired();
        builder.Property(u => u.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");
        builder.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_users_email");
    }
}
