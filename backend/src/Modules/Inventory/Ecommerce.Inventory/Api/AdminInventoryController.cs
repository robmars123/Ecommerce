using Ecommerce.Identity.Contracts;
using Ecommerce.Inventory.Application;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Api;

/// <summary>Admin only (rule 11). Thin: validate, call the service, map the outcome.</summary>
[ApiController]
[Route("api/v1/admin/inventory")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminInventoryController(
    InventoryAdminService inventory,
    IValidator<AdjustStockRequest> adjustValidator,
    ILogger<AdminInventoryController> logger) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet("")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        if (page is < 1)
        {
            ModelState.AddModelError("page", "page must be 1 or greater.");
        }
        if (pageSize is < 1 or > MaxPageSize)
        {
            ModelState.AddModelError("pageSize", $"pageSize must be between 1 and {MaxPageSize}.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok((await inventory.ListAsync(page ?? 1, pageSize ?? DefaultPageSize, ct)).ToResponse());
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust(AdjustStockRequest request, CancellationToken ct)
    {
        ValidationResult validation = await adjustValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (ValidationFailure error in validation.Errors)
            {
                string key = error.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..];
                ModelState.AddModelError(key, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        Guid actorId = Guid.Parse(User.FindFirst("sub")!.Value);
        AdjustResult result = await inventory.AdjustAsync(
            actorId, request.VariantId!.Value, request.Delta!.Value, request.ExpectedOnHand!.Value, request.Reason!.Trim(), ct);

        // Answerable from logs: which variant, what happened (no reason text).
        logger.LogInformation("Admin inventory adjust on {VariantId} finished with {Outcome}", request.VariantId, result.Outcome);
        switch (result.Outcome)
        {
            case AdjustOutcome.Ok:
                return Ok(result.Item!.ToResponse());
            case AdjustOutcome.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Variant not found");
            case AdjustOutcome.Stale:
                return ConflictProblem(result, "The on-hand count changed. Reload and try again.");
            default:
                return ConflictProblem(result, "The change would leave less available than is reserved.");
        }
    }

    private ObjectResult ConflictProblem(AdjustResult result, string detail)
    {
        ProblemDetails problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, StatusCodes.Status409Conflict, title: "Conflict", detail: detail);
        problem.Extensions["onHand"] = result.OnHand;
        problem.Extensions["reserved"] = result.Reserved;
        problem.Extensions["available"] = result.Available;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
    }
}
