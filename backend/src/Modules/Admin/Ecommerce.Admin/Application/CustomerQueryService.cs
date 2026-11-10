using Ecommerce.Identity.Contracts;
using Ecommerce.Orders.Contracts;
using Ecommerce.Payments.Contracts;

namespace Ecommerce.Admin.Application;

internal sealed record CustomerListView(IReadOnlyList<CustomerSummary> Items, int TotalCount);

/// <summary>The customer detail. <see cref="TotalSpent"/> is null when the caller may not see revenue (Support).</summary>
internal sealed record CustomerDetailView(
    CustomerSummary Customer,
    string Currency,
    decimal? TotalSpent,
    int OrderCount,
    IReadOnlyList<RecentOrder> RecentOrders,
    int ReturnCount,
    IReadOnlyList<CustomerReturn> RecentReturns,
    int RefundCount,
    IReadOnlyList<OrderRefund> RecentRefunds);

internal sealed record OrderRefund(OrderRefundInfo Refund, string OrderNumber);

/// <summary>
/// The admin customer screens (app-context 12.1). Three reads through the owning modules' contracts, no cross-module join (rule 8):
/// Identity (profile), Orders (orders, paid total, returns), Payments (refunds for those orders).
/// </summary>
internal sealed class CustomerQueryService(IUserReader users, IOrderReader orders, IRefundState refunds, DashboardSettings settings)
{
    public const int RecentTake = 10;

    public async Task<CustomerListView> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        CustomerPage result = await users.SearchCustomersAsync(search, page, pageSize, ct);
        return new CustomerListView(result.Items, result.TotalCount);
    }

    /// <summary>Total spent = paid orders (ever Paid) minus Succeeded refunds made by an admin. The caller decides whether to include it.</summary>
    public async Task<CustomerDetailView?> GetAsync(Guid id, bool includeTotal, CancellationToken ct)
    {
        CustomerSummary? customer = await users.GetCustomerAsync(id, ct);
        if (customer is null)
        {
            return null;
        }

        CustomerOrders customerOrders = await orders.GetCustomerOrdersAsync(id, RecentTake, ct);
        CustomerRefunds customerRefunds = customerOrders.All.Count == 0
            ? new CustomerRefunds(0m, 0, [])
            : await refunds.GetForOrdersAsync(customerOrders.All.Select(o => o.Id).ToList(), RecentTake, ct);

        Dictionary<Guid, string> numbers = customerOrders.All.ToDictionary(o => o.Id, o => o.OrderNumber);
        return new CustomerDetailView(
            customer,
            settings.Currency,
            includeTotal ? customerOrders.PaidTotal - customerRefunds.ActorSucceededTotal : null,
            customerOrders.OrderCount,
            customerOrders.Recent,
            customerOrders.ReturnCount,
            customerOrders.RecentReturns,
            customerRefunds.Count,
            customerRefunds.Recent.Select(r => new OrderRefund(r, numbers.GetValueOrDefault(r.OrderId, ""))).ToList());
    }
}
