using System.Security.Claims;
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

/// <summary>The owner asks to return part of a Delivered order (arch 4.7, D12). The owner is the token's <c>sub</c>. No money moves here.</summary>
internal static class ReturnsEndpoints
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("api/v1/orders/{id:guid}/return-request", RequestReturn).RequireAuthorization();

    private static async Task<Results<Created<ReturnCreatedResponse>, UnauthorizedHttpResult, ValidationProblem, ProblemHttpResult>> RequestReturn(
        Guid id,
        [FromBody] ReturnRequestBody body,
        ClaimsPrincipal user,
        [FromServices] IValidator<ReturnRequestBody> validator,
        [FromServices] ReturnService returns,
        CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirst("sub")?.Value, out Guid userId))
        {
            return TypedResults.Unauthorized();
        }
        ValidationResult validation = await validator.ValidateAsync(body, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(ToErrors(validation));
        }

        List<ReturnItem> items = body.Items!.Select(i => new ReturnItem(i.VariantId!.Value, i.Quantity!.Value)).ToList();
        ReturnRequestResult result = await returns.RequestAsync(userId, id, items, body.Reason!, ct);
        return result switch
        {
            ReturnRequestResult.Created created => TypedResults.Created($"/api/v1/orders/{id}", new ReturnCreatedResponse(created.ReturnId)),
            ReturnRequestResult.Forbidden => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden"),
            ReturnRequestResult.NotDelivered => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a delivered order can be returned", type: "not-delivered"),
            ReturnRequestResult.AlreadyOpen => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "This order already has an open return request", type: "return-already-open"),
            ReturnRequestResult.Invalid invalid => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] }),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found"),
        };
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(e => e.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
