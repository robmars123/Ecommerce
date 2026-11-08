using Ecommerce.Identity.Contracts;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Orders.Api;

internal sealed record ReturnCreatedResponse(Guid ReturnId);

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

/// <summary>The owner asks to return part of a Delivered order (arch 4.7, D12). No money moves here.</summary>
[ApiController]
[Route("api/v1/orders/{id:guid}/return-request")]
[Authorize]
internal sealed class ReturnsController(ReturnService returns, IValidator<ReturnRequestBody> validator) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> RequestReturn(Guid id, ReturnRequestBody body, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out Guid userId))
        {
            return Unauthorized();
        }
        ValidationResult validation = await validator.ValidateAsync(body, ct);
        if (!validation.IsValid)
        {
            return this.InvalidProblem(validation);
        }

        List<ReturnItem> items = body.Items!.Select(i => new ReturnItem(i.VariantId!.Value, i.Quantity!.Value)).ToList();
        switch (await returns.RequestAsync(userId, id, items, body.Reason!, ct))
        {
            case ReturnRequestResult.Created created:
                return Created($"/api/v1/orders/{id}", new ReturnCreatedResponse(created.ReturnId));
            case ReturnRequestResult.Forbidden:
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            case ReturnRequestResult.NotDelivered:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Only a delivered order can be returned", type: "not-delivered");
            case ReturnRequestResult.AlreadyOpen:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "This order already has an open return request", type: "return-already-open");
            case ReturnRequestResult.Invalid invalid:
                ModelState.AddModelError(invalid.Field, invalid.Message);
                return ValidationProblem(ModelState);
            default:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found");
        }
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

/// <summary>Order detail for the admin screen (D14): Admin and Support may read; only Admin acts (other controllers).</summary>
[ApiController]
[Route("api/v1/admin/orders/{id:guid}")]
[Authorize(Policy = AuthorizationPolicies.AdminOrSupport)]
internal sealed class AdminOrderDetailController(OrderQueryService orders) : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        AdminOrderDetailView? view = await orders.GetForAdminAsync(id, ct);
        return view is null ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found") : Ok(view.ToResponse());
    }
}
