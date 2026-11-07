using Ecommerce.Cart.Application;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Cart.Api;

/// <summary>
/// Any signed-in user owns exactly their own cart: the owner is the token's <c>sub</c>, and no route carries an id.
/// Thin: validate, call the service, map the outcome.
/// </summary>
[ApiController]
[Route("api/v1/cart")]
[Authorize]
internal sealed class CartController(
    CartService cart,
    CartMergeService merge,
    IValidator<AddCartItemRequest> addValidator,
    IValidator<SetCartItemRequest> setValidator,
    IValidator<MergeCartRequest> mergeValidator) : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok((await cart.GetAsync(UserId(), ct)).ToResponse());

    [HttpPost("items")]
    public async Task<IActionResult> Add(AddCartItemRequest request, CancellationToken ct)
    {
        ValidationResult validation = await addValidator.ValidateAsync(request, ct);
        return !validation.IsValid
            ? InvalidProblem(validation)
            : Map(await cart.AddAsync(UserId(), request.VariantId!.Value, request.Quantity!.Value, ct));
    }

    [HttpPut("items/{variantId:guid}")]
    public async Task<IActionResult> Set(Guid variantId, SetCartItemRequest request, CancellationToken ct)
    {
        ValidationResult validation = await setValidator.ValidateAsync(request, ct);
        return !validation.IsValid
            ? InvalidProblem(validation)
            : Map(await cart.SetAsync(UserId(), variantId, request.Quantity!.Value, ct));
    }

    [HttpDelete("items/{variantId:guid}")]
    public async Task<IActionResult> Remove(Guid variantId, CancellationToken ct)
    {
        await cart.RemoveAsync(UserId(), variantId, ct);
        return NoContent();
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge(
        MergeCartRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!MergeCartRequestValidator.IsValidKey(idempotencyKey))
        {
            ModelState.AddModelError("Idempotency-Key", $"Send an Idempotency-Key header of 1 to {MergeCartRequestValidator.MaxKeyLength} characters.");
            return ValidationProblem(ModelState);
        }
        ValidationResult validation = await mergeValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return InvalidProblem(validation);
        }

        List<MergeLine> lines = request.Lines!.Select(l => new MergeLine(l.VariantId!.Value, l.Quantity!.Value)).ToList();
        MergeReply reply = await merge.MergeAsync(UserId(), idempotencyKey!.Trim(), lines, ct);
        return reply.Status == MergeStatus.Ok
            ? Ok(reply.Result!.ToResponse())
            : Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict",
                detail: "This Idempotency-Key was already used for a different request.");
    }

    // The [Authorize] policy guarantees an authenticated user, so the token has a valid sub.
    private Guid UserId() => Guid.Parse(User.FindFirst("sub")!.Value);

    private IActionResult Map(CartWriteResult result)
    {
        switch (result.Outcome)
        {
            case CartOutcome.Ok:
                return Ok(result.Cart!.ToResponse());
            case CartOutcome.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Item not in cart");
            case CartOutcome.Insufficient:
                ProblemDetails problem = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext, StatusCodes.Status409Conflict, title: "Not enough stock",
                    detail: $"Only {result.Available} available.");
                problem.Extensions["available"] = result.Available;
                return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
            default:
                ModelState.AddModelError(result.Field ?? "", result.Message ?? "Invalid request.");
                return ValidationProblem(ModelState);
        }
    }

    private IActionResult InvalidProblem(ValidationResult result)
    {
        foreach (ValidationFailure error in result.Errors)
        {
            string key = error.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..];
            ModelState.AddModelError(key, error.ErrorMessage);
        }
        return ValidationProblem(ModelState);
    }
}
