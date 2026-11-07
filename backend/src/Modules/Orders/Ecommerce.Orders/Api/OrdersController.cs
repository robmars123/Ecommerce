using Ecommerce.Orders.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Orders.Api;

/// <summary>The caller's own orders (D10). The user id comes from the validated token, never from the request.</summary>
[ApiController]
[Route("api/v1/orders")]
[Authorize]
internal sealed class OrdersController(OrderQueryService orders, RetryPaymentService retry) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet("")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out Guid userId))
        {
            return Unauthorized();
        }
        int p = page ?? 1;
        int size = pageSize ?? DefaultPageSize;
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

        return Ok((await orders.ListAsync(userId, p, size, ct)).ToResponse());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out Guid userId))
        {
            return Unauthorized();
        }
        return await orders.GetAsync(userId, id, ct) switch
        {
            OrderReadResult<OrderDetailView>.Ok ok => Ok(ok.Value.ToResponse()),
            OrderReadResult<OrderDetailView>.Forbidden => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden"),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found"),
        };
    }

    [HttpPost("{id:guid}/retry-payment")]
    public async Task<IActionResult> RetryPayment(Guid id, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out Guid userId))
        {
            return Unauthorized();
        }
        if (!IdempotencyKeys.IsValid(idempotencyKey))
        {
            ModelState.AddModelError("Idempotency-Key", $"Send an Idempotency-Key header of 1 to {IdempotencyKeys.MaxLength} characters.");
            return ValidationProblem(ModelState);
        }

        switch (await retry.RetryAsync(userId, id, idempotencyKey!.Trim(), ct))
        {
            case RetryResult.Ok ok:
                return Ok(new RetryPaymentResponse(ok.OrderId, ok.Attempt, ok.ClientSecret));
            case RetryResult.Forbidden:
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            case RetryResult.NotPending:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "The order is not waiting for payment", type: "not-pending-payment");
            case RetryResult.ReservationExpired:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "The reservation has expired; check out again", type: "reservation-expired");
            case RetryResult.KeyConflict:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict",
                    detail: "This Idempotency-Key was already used for a different request.");
            case RetryResult.ProviderUnavailable unavailable:
                ProblemDetails problem = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext, StatusCodes.Status503ServiceUnavailable, title: "Payment is temporarily unavailable", type: "payment-provider-unavailable");
                problem.Extensions["orderId"] = unavailable.OrderId;
                return new ObjectResult(problem) { StatusCode = StatusCodes.Status503ServiceUnavailable };
            default:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found");
        }
    }
}
