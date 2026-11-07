using Ecommerce.Inventory.Application;
using Ecommerce.Inventory.Contracts;

namespace Ecommerce.Inventory.Api;

// Wire shape (arch 9, Admin inventory contract).
internal sealed record InventoryItemResponse(
    Guid VariantId, string ProductName, string Sku, string VariantName, bool IsActive,
    int OnHand, int Reserved, int Available, StockStatus StockStatus);

internal sealed record InventoryListResponse(IReadOnlyList<InventoryItemResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

internal static class InventoryResponseMapping
{
    public static InventoryItemResponse ToResponse(this InventoryItem i) =>
        new(i.VariantId, i.ProductName, i.Sku, i.VariantName, i.IsActive, i.OnHand, i.Reserved, i.Available, i.StockStatus);

    public static InventoryListResponse ToResponse(this InventoryPage p) =>
        new(p.Items.Select(ToResponse).ToList(), p.Page, p.PageSize, p.TotalCount, p.TotalPages);
}
