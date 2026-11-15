using Ecommerce.Identity.Contracts;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Orders.Api;

internal static class ControllerValidation
{
    public static ActionResult InvalidProblem(this ControllerBase controller, ValidationResult result)
    {
        foreach (ValidationFailure error in result.Errors)
        {
            string key = error.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..];
            controller.ModelState.AddModelError(key, error.ErrorMessage);
        }
        return controller.ValidationProblem(controller.ModelState);
    }
}

/// <summary>Admin returns queue (arch 9): list, approve, decline. Approval moves no money (D13).</summary>
[ApiController]
[Route("api/v1/admin/returns")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminReturnsController(ReturnService returns, IValidator<DecisionRequest> validator) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet("")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        int p = page ?? 1;
        int size = pageSize ?? DefaultPageSize;
        ReturnStatus? parsed = null;
        if (status is not null)
        {
            if (Enum.GetNames<ReturnStatus>().Contains(status) && Enum.TryParse(status, out ReturnStatus s))
            {
                parsed = s;
            }
            else
            {
                ModelState.AddModelError("status", "Choose a valid return status.");
            }
        }
        if (p < 1)
        {
            ModelState.AddModelError("page", "Page must be 1 or more.");
        }
        if (size is < 1 or > MaxPageSize)
        {
            ModelState.AddModelError("pageSize", $"Page size must be 1 to {MaxPageSize}.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        ReturnRows rows = await returns.ListAsync(parsed, p, size, ct);
        return Ok(new ReturnListResponse(rows.Items.Select(r => r.ToResponse()).ToList(), p, size, rows.TotalCount, (rows.TotalCount + size - 1) / size));
    }

    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, DecisionRequest body, CancellationToken ct) =>
        DecideAsync(id, body, (actor, note) => returns.ApproveAsync(actor, id, note, ct), ct);

    [HttpPost("{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id, DecisionRequest body, CancellationToken ct) =>
        DecideAsync(id, body, (actor, note) => returns.DeclineAsync(actor, id, note, ct), ct);

    private async Task<IActionResult> DecideAsync(Guid id, DecisionRequest body, Func<Guid, string, Task<DecisionResult>> decide, CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(body, ct);
        if (!validation.IsValid)
        {
            return this.InvalidProblem(validation);
        }

        DecisionResult result = await decide(Guid.Parse(User.FindFirst("sub")!.Value), body.Reason!.Trim());
        return result.Outcome switch
        {
            DecisionOutcome.Ok => NoContent(),
            DecisionOutcome.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Return request not found"),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.Message),
        };
    }
}
