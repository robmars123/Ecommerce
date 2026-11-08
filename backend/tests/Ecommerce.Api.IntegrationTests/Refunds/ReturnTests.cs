using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Refunds;

// Returns (arch 4.7, 7.3; Slice 6 D12, D13): the customer request and the admin queue.
public class ReturnTests(PostgresFixture postgres)
{
    private static object Body(Guid variant, int quantity = 1, string reason = "Wrong size") =>
        new { items = new[] { new { variantId = variant, quantity } }, reason };

    private static string Url(Guid order) => $"/api/v1/orders/{order}/return-request";

    private static async Task<(RefundEnv E, Guid Order, Guid Variant)> StartAsync(PostgresFixture postgres, string status = "Delivered")
    {
        RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync(status);
        return (e, order, await e.VariantOfAsync(order));
    }

    [Fact]
    public async Task The_owner_of_a_Delivered_order_creates_a_Requested_return()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;

        IdentityTestEnv.Api r = await e.Env.PostAsync(Url(order), Body(variant), e.TokenA);

        Assert.Equal(HttpStatusCode.Created, r.Status);
        Guid id = r.Json.GetProperty("returnId").GetGuid();
        Assert.Equal("Requested", await e.F.ScalarAsync<string>("SELECT status FROM return_requests WHERE id = @r", ("r", id)));
        Assert.Equal("Delivered", await e.F.OrderStatusAsync(order));
        Assert.Equal(0, await e.OpenRefundsCountAsync(order)); // a request moves no money
    }

    [Fact]
    public async Task T7_another_customers_order_is_403_anonymous_is_401_and_an_unknown_order_is_404_and_nothing_is_created()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;

        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.PostAsync(Url(order), Body(variant), e.TokenB)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.PostAsync(Url(order), Body(variant), null)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await e.Env.PostAsync(Url(Guid.NewGuid()), Body(variant), e.TokenA)).Status);
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM return_requests"));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Processing")]
    [InlineData("Shipped")]
    [InlineData("Returned")]
    public async Task A_return_on_an_order_that_is_not_Delivered_is_409(string status)
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres, status);
        await using RefundEnv env = e;

        Assert.Equal(HttpStatusCode.Conflict, (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Status);
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM return_requests"));
    }

    [Fact]
    public async Task Bad_items_quantities_or_reasons_are_400()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;
        object[] bad =
        [
            new { items = Array.Empty<object>(), reason = "x" },
            Body(variant, quantity: 0),
            Body(variant, quantity: 2), // the order has 1
            Body(Guid.NewGuid()), // not a line of this order
            new { items = new[] { new { variantId = variant, quantity = 1 }, new { variantId = variant, quantity = 1 } }, reason = "x" },
            Body(variant, reason: ""),
            Body(variant, reason: new string('x', 501)),
            new { items = new[] { new { variantId = variant, quantity = 1 } }, reason = "x", status = "Approved" },
        ];

        foreach (object body in bad)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await e.Env.PostAsync(Url(order), body, e.TokenA)).Status);
        }
        Assert.Equal(0, await e.F.CountAsync("SELECT count(*) FROM return_requests"));
    }

    [Fact]
    public async Task A_second_open_request_is_409_until_the_first_is_declined()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;
        Guid first = (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Json.GetProperty("returnId").GetGuid();

        Assert.Equal(HttpStatusCode.Conflict, (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await e.Env.PostAsync($"/api/v1/admin/returns/{first}/decline", new { reason = "Past the window" }, e.Admin)).Status);
        Assert.Equal(HttpStatusCode.Created, (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Status);
    }

    [Fact]
    public async Task A_double_submit_creates_one_request()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;

        IdentityTestEnv.Api[] results = await Task.WhenAll(
            e.Env.PostAsync(Url(order), Body(variant), e.TokenA), e.Env.PostAsync(Url(order), Body(variant), e.TokenA));

        Assert.Single(results, r => r.Status == HttpStatusCode.Created);
        Assert.Single(results, r => r.Status == HttpStatusCode.Conflict);
        Assert.Equal(1, await e.F.CountAsync("SELECT count(*) FROM return_requests"));
    }

    [Fact]
    public async Task Approve_and_decline_work_only_from_Requested_write_an_audit_row_and_move_no_money()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;
        Guid returnId = (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Json.GetProperty("returnId").GetGuid();
        Guid adminId = await e.F.ScalarAsync<Guid>("SELECT id FROM users WHERE email = 'admin@example.test'");

        Assert.Equal(HttpStatusCode.NoContent, (await e.Env.PostAsync($"/api/v1/admin/returns/{returnId}/approve", new { reason = "Within 30 days" }, e.Admin)).Status);

        Assert.Equal("Approved", await e.F.ScalarAsync<string>("SELECT status FROM return_requests WHERE id = @r", ("r", returnId)));
        Assert.Equal("Within 30 days", await e.F.ScalarAsync<string>("SELECT admin_note FROM return_requests WHERE id = @r", ("r", returnId)));
        Assert.Equal(adminId, await e.F.ScalarAsync<Guid>("SELECT actor_id FROM audit_log WHERE action = 'return.approve' AND entity_id = @r", ("r", returnId)));
        Assert.Equal(0, await e.OpenRefundsCountAsync(order));
        // Already decided: both a second approve and a decline are 409.
        Assert.Equal(HttpStatusCode.Conflict, (await e.Env.PostAsync($"/api/v1/admin/returns/{returnId}/approve", new { reason = "again" }, e.Admin)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await e.Env.PostAsync($"/api/v1/admin/returns/{returnId}/decline", new { reason = "changed my mind" }, e.Admin)).Status);
        Assert.Equal(1, await e.AuditCountAsync("return.approve"));
        Assert.Equal(0, await e.AuditCountAsync("return.decline"));
    }

    [Fact]
    public async Task Decisions_need_Admin_a_reason_and_an_existing_request()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;
        Guid returnId = (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Json.GetProperty("returnId").GetGuid();
        string approve = $"/api/v1/admin/returns/{returnId}/approve";

        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.PostAsync(approve, new { reason = "x" }, null)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.PostAsync(approve, new { reason = "x" }, e.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.PostAsync(approve, new { reason = "x" }, e.Support)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await e.Env.PostAsync(approve, new { reason = "" }, e.Admin)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await e.Env.PostAsync(approve, new { reason = new string('x', 501) }, e.Admin)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await e.Env.PostAsync($"/api/v1/admin/returns/{Guid.NewGuid()}/decline", new { reason = "x" }, e.Admin)).Status);
        Assert.Equal("Requested", await e.F.ScalarAsync<string>("SELECT status FROM return_requests WHERE id = @r", ("r", returnId)));
    }

    [Fact]
    public async Task The_admin_list_filters_by_status_pages_and_is_Admin_only()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync(postgres);
        await using RefundEnv env = e;
        Guid second = await e.SeedOrderAsync("Delivered");
        Guid firstReturn = (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Json.GetProperty("returnId").GetGuid();
        e.Env.Clock.UtcNow = e.Env.Clock.UtcNow.AddMinutes(1);
        Guid secondReturn = (await e.Env.PostAsync(Url(second), Body(await e.VariantOfAsync(second)), e.TokenA)).Json.GetProperty("returnId").GetGuid();
        await e.Env.PostAsync($"/api/v1/admin/returns/{firstReturn}/decline", new { reason = "no" }, e.Admin);

        IdentityTestEnv.Api all = await e.Env.GetAsync("/api/v1/admin/returns", e.Admin);
        IdentityTestEnv.Api requested = await e.Env.GetAsync("/api/v1/admin/returns?status=Requested", e.Admin);
        IdentityTestEnv.Api paged = await e.Env.GetAsync("/api/v1/admin/returns?pageSize=1&page=2", e.Admin);

        Assert.Equal(2, all.Json.GetProperty("totalCount").GetInt32());
        Assert.Equal(secondReturn, Assert.Single(requested.Json.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal("Black tee, M", requested.Json.GetProperty("items")[0].GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal(2, paged.Json.GetProperty("totalPages").GetInt32());
        Assert.Equal(firstReturn, Assert.Single(paged.Json.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Env.GetAsync("/api/v1/admin/returns", null)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync("/api/v1/admin/returns", e.TokenA)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await e.Env.GetAsync("/api/v1/admin/returns", e.Support)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await e.Env.GetAsync("/api/v1/admin/returns?status=Nope", e.Admin)).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await e.Env.GetAsync("/api/v1/admin/returns?page=0", e.Admin)).Status);
    }
}
