using Ecommerce.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Orders.Infrastructure.Persistence;

internal sealed class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("return_requests", t =>
            t.HasCheckConstraint("ck_return_requests_status", "status IN ('Requested','Approved','Declined','Refunded')"));
        builder.HasKey(r => r.Id).HasName("pk_return_requests");
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.OrderId).HasColumnName("order_id");
        builder.Property(r => r.ItemsJson).HasColumnName("items").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.AdminNote).HasColumnName("admin_note").HasMaxLength(500);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.DecidedAt).HasColumnName("decided_at");
        builder.Ignore(r => r.Items);

        builder.HasOne<Order>().WithMany().HasForeignKey(r => r.OrderId)
            .HasConstraintName("fk_return_requests_order_id").OnDelete(DeleteBehavior.Restrict);
        // One open request per order (D12): a double submit loses at the database, not just in code.
        builder.HasIndex(r => r.OrderId).IsUnique().HasDatabaseName("ux_return_requests_open_order_id")
            .HasFilter("status IN ('Requested','Approved')");
        builder.HasIndex(r => new { r.Status, r.CreatedAt }).HasDatabaseName("ix_return_requests_status_created_at");
    }
}
