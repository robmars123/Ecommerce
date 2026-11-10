using Ecommerce.Admin.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.Orders.Contracts;
using Ecommerce.Payments.Contracts;

namespace Ecommerce.Admin.Tests;

// Slice 12.1 U1, U2: the total-spent arithmetic and the order-number mapping, with fake readers.
public class CustomerQueryServiceTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeUsers : IUserReader
    {
        public Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) => throw new NotSupportedException();

        public Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();

        public Task<CustomerPage> SearchCustomersAsync(string? search, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<CustomerSummary?> GetCustomerAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<CustomerSummary?>(new CustomerSummary(userId, "buyer@example.test", "Buyer", Now));
    }

    private sealed class FakeOrders(CustomerOrders result) : IOrderReader
    {
        public Task<OrderNotificationView?> GetNotificationViewAsync(Guid orderId, CancellationToken ct) => throw new NotSupportedException();

        public Task<RecentOrderPage> ListRecentAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<int> CountPendingReturnsAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<CustomerOrders> GetCustomerOrdersAsync(Guid userId, int recentTake, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class FakeRefunds(CustomerRefunds result) : IRefundState
    {
        public int Calls { get; private set; }

        public Task<bool> HasPendingFullRefundAsync(Guid orderId, CancellationToken ct) => throw new NotSupportedException();

        public Task<RefundSummary> GetSummaryAsync(Guid orderId, CancellationToken ct) => throw new NotSupportedException();

        public Task<CustomerRefunds> GetForOrdersAsync(IReadOnlyCollection<Guid> orderIds, int recentTake, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private static CustomerQueryService Service(CustomerOrders orders, FakeRefunds refunds) =>
        new(new FakeUsers(), new FakeOrders(orders), refunds, new DashboardSettings("USD"));

    [Fact]
    public async Task U1_total_spent_is_paid_minus_the_actor_refunds_and_null_when_not_included_and_refunds_carry_the_order_number()
    {
        CustomerOrders orders = new(1, 140.00m, [new CustomerOrderRef(OrderId, "ORD-2026-000001")], [], 0, []);
        CustomerRefunds refunds = new(30.00m, 1, [new OrderRefundInfo(Guid.NewGuid(), OrderId, 30.00m, "USD", "Succeeded", false, Now)]);
        CustomerQueryService service = Service(orders, new FakeRefunds(refunds));

        CustomerDetailView? withTotal = await service.GetAsync(CustomerId, true, TestContext.Current.CancellationToken);
        CustomerDetailView? withoutTotal = await service.GetAsync(CustomerId, false, TestContext.Current.CancellationToken);

        Assert.Equal(110.00m, withTotal!.TotalSpent);
        Assert.Equal("USD", withTotal.Currency);
        Assert.Equal("ORD-2026-000001", withTotal.RecentRefunds[0].OrderNumber);
        Assert.Null(withoutTotal!.TotalSpent);
    }

    [Fact]
    public async Task U2_payments_is_not_called_when_the_customer_has_no_orders()
    {
        FakeRefunds refunds = new(new CustomerRefunds(0m, 0, []));
        CustomerQueryService service = Service(new CustomerOrders(0, 0m, [], [], 0, []), refunds);

        CustomerDetailView? view = await service.GetAsync(CustomerId, true, TestContext.Current.CancellationToken);

        Assert.Equal(0, refunds.Calls);
        Assert.Equal(0m, view!.TotalSpent);
    }
}
