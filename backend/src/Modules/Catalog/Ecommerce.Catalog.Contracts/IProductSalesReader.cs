namespace Ecommerce.Catalog.Contracts;

public sealed record TopProduct(Guid ProductId, string Name, int Units);

/// <summary>Catalog's sales rank, projected from OrderPaid events (arch 8.8, Slice 6b D15, D16). Eventually consistent.</summary>
public interface IProductSalesReader
{
    /// <summary>Units sold per product on the UTC dates fromDate to toDate inclusive, most units first, then product id.</summary>
    Task<IReadOnlyList<TopProduct>> GetTopAsync(DateOnly fromDate, DateOnly toDate, int count, CancellationToken ct);
}
