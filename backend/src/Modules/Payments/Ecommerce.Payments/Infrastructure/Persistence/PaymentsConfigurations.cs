using Ecommerce.Payments.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Payments.Infrastructure.Persistence;

internal sealed class PaymentsDbModel : IModuleDbModel
{
    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PaymentsDbModel).Assembly,
            t => t.Namespace == typeof(PaymentsDbModel).Namespace);
}

// No foreign key from payments to orders: they live in different modules (arch 6.2); the order id is a plain reference.
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", t =>
        {
            t.HasCheckConstraint("ck_payments_status", "status IN ('Pending','Succeeded','Failed','PartiallyRefunded','Refunded')");
            t.HasCheckConstraint("ck_payments_attempt", "attempt >= 1");
            t.HasCheckConstraint("ck_payments_amount", "amount >= 0");
        });
        builder.HasKey(p => p.Id).HasName("pk_payments");
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.OrderId).HasColumnName("order_id");
        builder.Property(p => p.Attempt).HasColumnName("attempt");
        builder.Property(p => p.Provider).HasColumnName("provider").HasMaxLength(30).IsRequired();
        builder.Property(p => p.ProviderPaymentId).HasColumnName("provider_payment_id").HasMaxLength(100);
        builder.Property(p => p.Amount).HasColumnName("amount").HasColumnType("numeric(19,4)");
        builder.Property(p => p.Currency).HasColumnName("currency").HasColumnType("character(3)").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Ignore(p => p.HasCancellableIntent);

        builder.HasIndex(p => new { p.OrderId, p.Attempt }).IsUnique().HasDatabaseName("ux_payments_order_id_attempt");
        builder.HasIndex(p => p.ProviderPaymentId).IsUnique().HasDatabaseName("ux_payments_provider_payment_id");
    }
}

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds", t =>
        {
            t.HasCheckConstraint("ck_refunds_status", "status IN ('Pending','Succeeded','Failed')");
            t.HasCheckConstraint("ck_refunds_amount", "amount > 0");
        });
        builder.HasKey(r => r.Id).HasName("pk_refunds");
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.PaymentId).HasColumnName("payment_id");
        builder.Property(r => r.Amount).HasColumnName("amount").HasColumnType("numeric(19,4)");
        builder.Property(r => r.Currency).HasColumnName("currency").HasColumnType("character(3)").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(r => r.ActorId).HasColumnName("actor_id");
        builder.Property(r => r.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
        builder.Property(r => r.ProviderRefundId).HasColumnName("provider_refund_id").HasMaxLength(100);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");

        builder.HasOne<Payment>().WithMany().HasForeignKey(r => r.PaymentId)
            .HasConstraintName("fk_refunds_payment_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.IdempotencyKey).IsUnique().HasDatabaseName("ux_refunds_idempotency_key");
        builder.HasIndex(r => r.ProviderRefundId).IsUnique().HasDatabaseName("ux_refunds_provider_refund_id");
        builder.HasIndex(r => r.PaymentId).HasDatabaseName("ix_refunds_payment_id");
    }
}
