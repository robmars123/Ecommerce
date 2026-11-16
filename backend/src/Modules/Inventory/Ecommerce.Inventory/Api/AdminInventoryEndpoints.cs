using System.Security.Claims;
using Ecommerce.Identity.Contracts;
using Ecommerce.Inventory.Application;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Api;

/// <summary>Admin only (rule 11). Thin: validate, call the service once, map the outcome.</summary>
internal static class AdminInventoryEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("inventory", List);
        group.MapPost("inventory/adjust", Adjust);
    }

    private static async Task<Results<Ok<InventoryListResponse>, ValidationProblem>> List(
        [AsParameters] InventoryListQuery query,
        [FromServices] InventoryAdminService inventory,
        CancellationToken ct)
    {
        // Same order as MVC: a value that is not a number is reported alone, before the range checks.
        Dictionary<string, string[]> errors = [];
        int? pageNumber = QueryValues.ReadInt(query.Page, "page", errors);
        int? size = QueryValues.ReadInt(query.PageSize, "pageSize", errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (pageNumber is < 1)
        {
            errors["page"] = ["page must be 1 or greater."];
        }
        if (size is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Ok((await inventory.ListAsync(pageNumber ?? 1, size ?? DefaultPageSize, ct)).ToResponse());
    }

    private static async Task<Results<Ok<InventoryItemResponse>, ValidationProblem, ProblemHttpResult>> Adjust(
        [FromBody] AdjustStockRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<AdjustStockRequest> validator,
        [FromServices] InventoryAdminService inventory,
        [FromServices] ILoggerFactory loggers,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(ToErrors(validation));
        }

        Guid actorId = Guid.Parse(user.FindFirst("sub")!.Value);
        AdjustResult result = await inventory.AdjustAsync(
            actorId, request.VariantId!.Value, request.Delta!.Value, request.ExpectedOnHand!.Value, request.Reason!.Trim(), ct);

        // Answerable from logs: which variant, what happened (no reason text).
        loggers.CreateLogger(typeof(AdminInventoryEndpoints))
            .LogInformation("Admin inventory adjust on {VariantId} finished with {Outcome}", request.VariantId, result.Outcome);
        return result.Outcome switch
        {
            AdjustOutcome.Ok => TypedResults.Ok(result.Item!.ToResponse()),
            AdjustOutcome.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Variant not found"),
            AdjustOutcome.Stale => Conflict(result, "The on-hand count changed. Reload and try again."),
            _ => Conflict(result, "The change would leave less available than is reserved."),
        };
    }

    private static ProblemHttpResult Conflict(AdjustResult result, string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["onHand"] = result.OnHand,
                ["reserved"] = result.Reserved,
                ["available"] = result.Available,
            });

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(e => e.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
