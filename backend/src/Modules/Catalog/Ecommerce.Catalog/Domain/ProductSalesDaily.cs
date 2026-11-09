namespace Ecommerce.Catalog.Domain;

/// <summary>Units sold of a product on one UTC day, projected from OrderPaid events (Slice 6b D15). Feeds best sellers and the dashboard top products.</summary>
internal sealed class ProductSalesDaily
{
    public Guid ProductId { get; set; }
    public DateOnly SalesDate { get; set; }
    public int Units { get; set; }
}
