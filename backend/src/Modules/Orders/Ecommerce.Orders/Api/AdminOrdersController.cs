using Ecommerce.Identity.Contracts;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Orders.Api;

/// <summary>Admin only (rule 11). Thin: validate, call the service, map the outcome. Success is 204 (D12).</summary>
[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminOrdersController(
    OrderFulfillmentService fulfillment,
    IValidator<ShipOrderRequest> shipValidator,
    IValidator<StatusOverrideRequest> overrideValidator,
    ILogger<AdminOrdersController> logger) : ControllerBase
{
    [HttpPost("{id:guid}/process")]
    public async Task<IActionResult> Process(Guid id, CancellationToken ct) =>
        Map("process", id, await fulfillment.ProcessAsync(ActorId(), id, ct));

    [HttpPost("{id:guid}/ship")]
    public async Task<IActionResult> Ship(Guid id, ShipOrderRequest request, CancellationToken ct)
    {
        ValidationResult validation = await shipValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return InvalidProblem(validation);
        }
        return Map("ship", id, await fulfillment.ShipAsync(ActorId(), id, request.CarrierCode!, request.TrackingNumber!.Trim(), ct));
    }

    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct) =>
        Map("deliver", id, await fulfillment.DeliverAsync(ActorId(), id, ct));

    [HttpPost("{id:guid}/status-override")]
    public async Task<IActionResult> StatusOverride(Guid id, StatusOverrideRequest request, CancellationToken ct)
    {
        ValidationResult validation = await overrideValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return InvalidProblem(validation);
        }
        StatusOverrideRequestValidator.TryParseStatus(request.ToStatus, out OrderStatus to);
        return Map("status-override", id, await fulfillment.OverrideAsync(ActorId(), id, to, request.Reason!.Trim(), ct));
    }

    // The policy guarantees an authenticated admin, so the token has a valid sub.
    private Guid ActorId() => Guid.Parse(User.FindFirst("sub")!.Value);

    private IActionResult Map(string action, Guid orderId, FulfillmentResult result)
    {
        // Answerable from logs: which order, which action, what happened (no body, no reason text).
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object> { ["OrderId"] = orderId });
        logger.LogInformation("Admin order {Action} finished with {Outcome}", action, result.Outcome);
        switch (result.Outcome)
        {
            case FulfillmentOutcome.Ok:
                return NoContent();
            case FulfillmentOutcome.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found");
            case FulfillmentOutcome.Invalid:
                ModelState.AddModelError(result.Field ?? "", result.Message ?? "Invalid request.");
                return ValidationProblem(ModelState);
            default:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.Message);
        }
    }

    private ActionResult InvalidProblem(ValidationResult result)
    {
        foreach (ValidationFailure error in result.Errors)
        {
            string key = error.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..];
            ModelState.AddModelError(key, error.ErrorMessage);
        }
        return ValidationProblem(ModelState);
    }
}
