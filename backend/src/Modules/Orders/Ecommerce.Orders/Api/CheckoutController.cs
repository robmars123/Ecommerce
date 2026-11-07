using System.Globalization;
using System.Text.Json.Serialization;
using Ecommerce.Orders.Application;
using Ecommerce.Orders.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Orders.Api;

internal sealed record RejectedLineResponse(
    Guid VariantId, string Reason, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Available);

/// <summary>
/// Checkout and its menu of shipping methods. Any signed-in user shops (Slice 3 D1); the user id is the token sub.
/// Thin: validate, call the service, map the outcome. Problem <c>type</c> carries the machine-readable code.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
internal sealed class CheckoutController(
    CheckoutService checkout,
    ShippingMethods shipping,
    CheckoutSettings settings,
    IValidator<CheckoutRequest> validator) : ControllerBase
{
    [HttpGet("shipping-methods")]
    public IActionResult Methods() =>
        Ok(new ShippingMethodsResponse(
            shipping.All.Select(m => new ShippingMethodItemResponse(m.Code, m.Name, MoneyResponse.From(m.Amount, settings.Currency), m.EstimatedDays)).ToList()));

    [HttpPost("checkout")]
    public async Task<IActionResult> Place(
        CheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!IdempotencyKeys.IsValid(idempotencyKey))
        {
            ModelState.AddModelError("Idempotency-Key", $"Send an Idempotency-Key header of 1 to {IdempotencyKeys.MaxLength} characters.");
            return ValidationProblem(ModelState);
        }
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (ValidationFailure error in validation.Errors)
            {
                ModelState.AddModelError(CamelPath(error.PropertyName), error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        ShippingAddressRequest a = request.ShippingAddress!;
        ShippingAddress address = new(
            a.FullName!.Trim(), a.Line1!.Trim(), Clean(a.Line2), a.City!.Trim(), Clean(a.Region), a.PostalCode!.Trim(), a.Country!.Trim());
        CheckoutResult result = await checkout.CheckoutAsync(
            new CheckoutCommand(UserId(), idempotencyKey!.Trim(), address, request.ShippingMethodCode!.Trim()), ct);

        switch (result)
        {
            case CheckoutResult.Ok ok:
                return Ok(ok.ToResponse());
            case CheckoutResult.KeyConflict:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict",
                    detail: "This Idempotency-Key was already used for a different request.");
            case CheckoutResult.CartEmpty:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Your cart is empty", type: "cart-empty");
            case CheckoutResult.LinesRejected rejected:
                return WithExtension(
                    StatusCodes.Status409Conflict, "Some items cannot be ordered", "checkout-lines-rejected",
                    "lines", rejected.Lines.Select(l => new RejectedLineResponse(l.VariantId, l.Reason, l.Available)).ToList());
            case CheckoutResult.ProviderUnavailable unavailable:
                return WithExtension(
                    StatusCodes.Status503ServiceUnavailable, "Payment is temporarily unavailable", "payment-provider-unavailable",
                    "orderId", unavailable.OrderId);
            case CheckoutResult.Invalid invalid:
                ModelState.AddModelError(invalid.Field, invalid.Message);
                return ValidationProblem(ModelState);
            default:
                throw new InvalidOperationException("Unhandled checkout result.");
        }
    }

    private IActionResult WithExtension(int status, string title, string type, string name, object value)
    {
        ProblemDetails problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title: title, type: type);
        problem.Extensions[name] = value;
        return new ObjectResult(problem) { StatusCode = status };
    }

    private Guid UserId() => Guid.Parse(User.FindFirst("sub")!.Value);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"ShippingAddress.FullName" to "shippingAddress.fullName": the wire names the client binds errors to.</summary>
    private static string CamelPath(string path) =>
        string.Join('.', path.Split('.').Select(s => s.Length == 0 ? s : char.ToLower(s[0], CultureInfo.InvariantCulture) + s[1..]));
}
