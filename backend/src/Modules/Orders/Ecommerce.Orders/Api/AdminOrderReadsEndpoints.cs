using Ecommerce.Identity.Contracts;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Orders.Api;

/// <summary>Order list and detail for the admin screens (D14, 13.2): Admin and Support may read; only Admin acts (other endpoints).</summary>
internal static class AdminOrderReadsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.AdminOrSupport);
        group.MapGet("orders", List);
        group.MapGet("orders/{id:guid}", Get);
    }

    private static async Task<Results<Ok<AdminOrderListResponse>, ValidationProblem>> List(
        [AsParameters] AdminOrderListRawQuery raw,
        [FromServices] OrderQueryService orders,
        [FromServices] IValidator<AdminOrderListQuery> validator,
        CancellationToken ct)
    {
        // Bad numbers and dates are reported alone, before the validator, with MVC's PascalCase keys.
        Dictionary<string, string[]> bindingErrors = new Dictionary<string, string[]>();
        int? pageNumber = QueryValues.ReadInt(raw.Page, "Page", bindingErrors);
        int? pageSizeNumber = QueryValues.ReadInt(raw.PageSize, "PageSize", bindingErrors);
        DateOnly? fromDate = QueryValues.ReadDate(raw.From, "From", bindingErrors);
        DateOnly? toDate = QueryValues.ReadDate(raw.To, "To", bindingErrors);
        if (bindingErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(bindingErrors);
        }

        AdminOrderListQuery query = new AdminOrderListQuery(
            pageNumber, pageSizeNumber, FirstText(raw.Status), fromDate, toDate, FirstText(raw.Customer), FirstText(raw.OrderNumber));
        ValidationResult validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(ToErrors(validation));
        }

        OrderStatus? orderStatus = null;
        if (query.Status is not null)
        {
            StatusOverrideRequestValidator.TryParseStatus(query.Status, out OrderStatus parsed);
            orderStatus = parsed;
        }
        AdminOrderFilter filter = new AdminOrderFilter(orderStatus, query.From, query.To, null, query.OrderNumber?.Trim());
        AdminOrderListPage result = await orders.ListForAdminAsync(
            filter, query.Customer?.Trim(), query.Page ?? 1, query.PageSize ?? AdminOrderListQueryValidator.DefaultPageSize, ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<Results<Ok<AdminOrderDetailResponse>, ProblemHttpResult>> Get(
        Guid id, [FromServices] OrderQueryService orders, CancellationToken ct)
    {
        AdminOrderDetailView? view = await orders.GetForAdminAsync(id, ct);
        return view is null ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found") : TypedResults.Ok(view.ToResponse());
    }

    // First value of a query key; blank is null (as MVC binding did).
    private static string? FirstText(string[]? values)
    {
        string? raw = values?.FirstOrDefault();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(e => e.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
