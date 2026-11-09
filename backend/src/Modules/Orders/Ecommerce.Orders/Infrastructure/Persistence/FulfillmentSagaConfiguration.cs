using Ecommerce.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Orders.Infrastructure.Persistence;

internal sealed class FulfillmentSagaConfiguration : IEntityTypeConfiguration<FulfillmentSaga>
{
    public void Configure(EntityTypeBuilder<FulfillmentSaga> builder)
    {
        builder.ToTable("fulfillment_sagas", t =>
            t.HasCheckConstraint("ck_fulfillment_sagas_status", "status IN ('Compensating','Compensated')"));
        builder.HasKey(s => s.Id).HasName("pk_fulfillment_sagas");
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.OrderId).HasColumnName("order_id");
        builder.Property(s => s.Step).HasColumnName("step").HasMaxLength(30).IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(s => s.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(s => s.ActorId).HasColumnName("actor_id");
        builder.Property(s => s.RefundAmount).HasColumnName("refund_amount").HasColumnType("numeric(19,4)");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.HasOne<Order>().WithMany().HasForeignKey(s => s.OrderId)
            .HasConstraintName("fk_fulfillment_sagas_order_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.OrderId).IsUnique().HasDatabaseName("ux_fulfillment_sagas_order_id");
    }
}
