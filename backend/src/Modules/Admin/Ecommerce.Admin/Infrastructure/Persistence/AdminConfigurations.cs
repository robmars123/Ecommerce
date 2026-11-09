using Ecommerce.Admin.Domain;
using Ecommerce.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Admin.Infrastructure.Persistence;

internal sealed class AdminDbModel : IModuleDbModel
{
    public void Configure(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AdminDbModel).Assembly,
            t => t.Namespace == typeof(AdminDbModel).Namespace);
}

internal sealed class DashboardOrderConfiguration : IEntityTypeConfiguration<DashboardOrder>
{
    public void Configure(EntityTypeBuilder<DashboardOrder> builder)
    {
        builder.ToTable("admin_dashboard_orders");
        builder.HasKey(o => o.OrderId).HasName("pk_admin_dashboard_orders");
        builder.Property(o => o.OrderId).HasColumnName("order_id").ValueGeneratedNever();
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(o => o.GrandTotal).HasColumnName("grand_total").HasColumnType("numeric(19,4)");
        builder.Property(o => o.Currency).HasColumnName("currency").HasColumnType("character(3)");
        builder.Property(o => o.PaidAt).HasColumnName("paid_at");
        builder.HasIndex(o => o.PaidAt).HasDatabaseName("ix_admin_dashboard_orders_paid_at");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_admin_dashboard_orders_status");
    }
}

internal sealed class DashboardRefundConfiguration : IEntityTypeConfiguration<DashboardRefund>
{
    public void Configure(EntityTypeBuilder<DashboardRefund> builder)
    {
        builder.ToTable("admin_dashboard_refunds");
        builder.HasKey(r => r.RefundId).HasName("pk_admin_dashboard_refunds");
        builder.Property(r => r.RefundId).HasColumnName("refund_id").ValueGeneratedNever();
        builder.Property(r => r.OrderId).HasColumnName("order_id");
        builder.Property(r => r.Amount).HasColumnName("amount").HasColumnType("numeric(19,4)");
        builder.Property(r => r.SucceededAt).HasColumnName("succeeded_at");
        builder.HasIndex(r => r.SucceededAt).HasDatabaseName("ix_admin_dashboard_refunds_succeeded_at");
    }
}
