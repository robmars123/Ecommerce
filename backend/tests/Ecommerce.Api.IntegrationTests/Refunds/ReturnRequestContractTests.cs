using System.Net;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Refunds;

// P2.74 pins: the MVC contract of POST orders/{id}/return-request, written and green before the Minimal API conversion.
public class ReturnRequestContractTests(PostgresFixture postgres)
{
    private const string Rfc = "https://tools.ietf.org/html/rfc9110#section-";
    private const string ItemsMessage = "Choose 1 to 100 items to return.";
    private const string ReasonMessage = "Enter a reason of 1 to 500 characters.";

    private static string Url(Guid order) => $"/api/v1/orders/{order}/return-request";

    private static object Body(Guid variant, int quantity = 1) =>
        new { items = new[] { new { variantId = variant, quantity } }, reason = "Wrong size" };

    private async Task<(RefundEnv E, Guid Order, Guid Variant)> StartAsync(string status = "Delivered")
    {
        RefundEnv e = await RefundEnv.StartAsync(postgres);
        Guid order = await e.SeedOrderAsync(status);
        return (e, order, await e.VariantOfAsync(order));
    }

    private static void AssertProblem(IdentityTestEnv.Api r, HttpStatusCode status, string type)
    {
        Assert.Equal(status, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(type, r.Json.GetProperty("type").GetString());
        Assert.False(string.IsNullOrEmpty(r.Json.GetProperty("traceId").GetString()));
        Assert.Equal("id-test-1", r.Json.GetProperty("correlationId").GetString());
    }

    private static void AssertErrors(IdentityTestEnv.Api r, params (string Key, string Message)[] expected)
    {
        AssertProblem(r, HttpStatusCode.BadRequest, Rfc + "15.5.1");
        Assert.Equal("One or more validation errors occurred.", r.Json.GetProperty("title").GetString());
        List<string> actual = r.Json.GetProperty("errors").EnumerateObject()
            .Select(p => p.Name + "=" + Assert.Single(p.Value.EnumerateArray()).GetString())
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(expected.Select(x => x.Key + "=" + x.Message).OrderBy(x => x, StringComparer.Ordinal).ToList(), actual);
    }

    private static Task<long> RowsAsync(RefundEnv e) => e.F.CountAsync("SELECT count(*) FROM return_requests");

    [Fact]
    public async Task RR1_ownership_another_customer_is_403_unknown_is_404_anonymous_is_401_and_nothing_is_created()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync();
        await using RefundEnv env = e;

        IdentityTestEnv.Api other = await e.Env.PostAsync(Url(order), Body(variant), e.TokenB);
        IdentityTestEnv.Api unknown = await e.Env.PostAsync(Url(Guid.NewGuid()), Body(variant), e.TokenA);
        IdentityTestEnv.Api anonymous = await e.Env.PostAsync(Url(order), Body(variant), null);

        AssertProblem(other, HttpStatusCode.Forbidden, Rfc + "15.5.4");
        Assert.Equal("Forbidden", other.Json.GetProperty("title").GetString());
        Assert.False(other.Json.TryGetProperty("errors", out _));
        AssertProblem(unknown, HttpStatusCode.NotFound, Rfc + "15.5.5");
        Assert.Equal("Order not found", unknown.Json.GetProperty("title").GetString());
        AssertProblem(anonymous, HttpStatusCode.Unauthorized, Rfc + "15.5.2");
        Assert.Equal(0, await RowsAsync(e));
        Assert.Equal("Delivered", await e.F.OrderStatusAsync(order));
    }

    [Fact]
    public async Task RR2_check_order_a_bad_body_on_someone_elses_order_is_400_not_403()
    {
        (RefundEnv e, Guid order, _) = await StartAsync();
        await using RefundEnv env = e;

        IdentityTestEnv.Api r = await e.Env.PostAsync(Url(order), "{}", e.TokenB);

        AssertErrors(r, ("items", ItemsMessage), ("reason", ReasonMessage));
        Assert.Equal(0, await RowsAsync(e));
    }

    [Fact]
    public async Task RR3_a_not_Delivered_order_and_a_second_open_request_are_409_with_their_own_type()
    {
        (RefundEnv e, Guid shipped, Guid variant) = await StartAsync("Shipped");
        await using RefundEnv env = e;
        Guid delivered = await e.SeedOrderAsync("Delivered");
        Guid deliveredVariant = await e.VariantOfAsync(delivered);

        IdentityTestEnv.Api notDelivered = await e.Env.PostAsync(Url(shipped), Body(variant), e.TokenA);
        Assert.Equal(0, await RowsAsync(e));
        IdentityTestEnv.Api first = await e.Env.PostAsync(Url(delivered), Body(deliveredVariant), e.TokenA);
        IdentityTestEnv.Api second = await e.Env.PostAsync(Url(delivered), Body(deliveredVariant), e.TokenA);

        AssertProblem(notDelivered, HttpStatusCode.Conflict, "not-delivered");
        Assert.Equal("Only a delivered order can be returned", notDelivered.Json.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Created, first.Status);
        AssertProblem(second, HttpStatusCode.Conflict, "return-already-open");
        Assert.Equal("This order already has an open return request", second.Json.GetProperty("title").GetString());
        foreach (IdentityTestEnv.Api r in new[] { notDelivered, second })
        {
            Assert.False(r.Json.TryGetProperty("errors", out _));
            Assert.False(r.Json.TryGetProperty("detail", out _));
        }
        Assert.Equal(1, await RowsAsync(e));
    }

    [Fact]
    public async Task RR4_the_validator_rejects_with_these_keys_and_messages()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync();
        await using RefundEnv env = e;

        AssertErrors(await e.Env.PostAsync(Url(order), "{}", e.TokenA), ("items", ItemsMessage), ("reason", ReasonMessage));
        AssertErrors(
            await e.Env.PostAsync(Url(order), "{\"items\":[{}],\"reason\":\"x\"}", e.TokenA),
            ("items[0].VariantId", "Each item needs a variantId."), ("items[0].Quantity", "'Quantity' must not be empty."));
        AssertErrors(
            await e.Env.PostAsync(Url(order), "{\"items\":[{\"variantId\":\"" + variant + "\",\"quantity\":0}],\"reason\":\"   \"}", e.TokenA),
            ("items[0].Quantity", "Each item needs a quantity of 1 or more."), ("reason", ReasonMessage));
        Assert.Equal(0, await RowsAsync(e));
    }

    [Fact]
    public async Task RR5_the_service_rejects_lines_that_are_not_returnable_keyed_items()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync();
        await using RefundEnv env = e;
        object twice = new { items = new[] { new { variantId = variant, quantity = 1 }, new { variantId = variant, quantity = 1 } }, reason = "Wrong size" };

        AssertErrors(await e.Env.PostAsync(Url(order), Body(variant, 2), e.TokenA), ("items", "Return 1 to 1 of Black tee, M."));
        AssertErrors(await e.Env.PostAsync(Url(order), Body(Guid.NewGuid()), e.TokenA), ("items", "Only items from this order can be returned."));
        AssertErrors(await e.Env.PostAsync(Url(order), twice, e.TokenA), ("items", "List each item once."));
        Assert.Equal(0, await RowsAsync(e));
        Assert.Equal("Delivered", await e.F.OrderStatusAsync(order));
    }

    [Fact]
    public async Task RR6_the_happy_path_is_201_with_a_Location_and_only_returnId()
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync();
        await using RefundEnv env = e;
        DateTimeOffset now = e.Env.Clock.UtcNow;

        IdentityTestEnv.Api r = await e.Env.PostAsync(Url(order), Body(variant), e.TokenA);

        Assert.Equal(HttpStatusCode.Created, r.Status);
        Assert.Equal($"/api/v1/orders/{order}", r.Response.Headers.Location?.OriginalString);
        Assert.Equal(["returnId"], r.Json.EnumerateObject().Select(p => p.Name).ToList());
        Guid id = r.Json.GetProperty("returnId").GetGuid();
        Assert.Equal("Requested", await e.F.ScalarAsync<string>("SELECT status FROM return_requests WHERE id = @r", ("r", id)));
        Assert.Equal("Wrong size", await e.F.ScalarAsync<string>("SELECT reason FROM return_requests WHERE id = @r", ("r", id)));
        Assert.Equal(now, await e.F.ScalarAsync<DateTimeOffset>("SELECT created_at FROM return_requests WHERE id = @r", ("r", id)));
    }

    [Theory]
    [InlineData("{\"items\":")]
    [InlineData("null")]
    [InlineData("{\"items\":\"x\",\"reason\":\"y\"}")]
    public async Task RR7_an_unreadable_body_is_a_400_problem_and_creates_nothing(string body)
    {
        (RefundEnv e, Guid order, Guid variant) = await StartAsync();
        await using RefundEnv env = e;

        IdentityTestEnv.Api r = await e.Env.PostAsync(Url(order), body, e.TokenA);

        Assert.Equal(HttpStatusCode.BadRequest, r.Status);
        Assert.Equal("application/problem+json", r.Response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, await RowsAsync(e));
        Assert.Equal(HttpStatusCode.Created, (await e.Env.PostAsync(Url(order), Body(variant), e.TokenA)).Status);
    }
}
