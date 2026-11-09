using Ecommerce.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Catalog.Infrastructure.Persistence;

internal sealed class ProductSalesDailyConfiguration : IEntityTypeConfiguration<ProductSalesDaily>
{
    public void Configure(EntityTypeBuilder<ProductSalesDaily> builder)
    {
        builder.ToTable("catalog_product_sales_daily");
        builder.HasKey(s => new { s.ProductId, s.SalesDate }).HasName("pk_catalog_product_sales_daily");
        builder.Property(s => s.ProductId).HasColumnName("product_id");
        builder.Property(s => s.SalesDate).HasColumnName("sales_date");
        builder.Property(s => s.Units).HasColumnName("units");
    }
}
