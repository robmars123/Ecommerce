using System.Globalization;
using Ecommerce.Identity.Contracts;
using Ecommerce.Payments.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Payments.Api;

internal sealed record MoneyBody(string? Amount, string? Currency);

internal sealed record RefundBody(MoneyBody? Amount, string? Reason);

internal sealed record MoneyOut(string Amount, string Currency)
{
    public static MoneyOut From(decimal amount, string currency) => new(amount.ToString("0.00", CultureInfo.InvariantCulture), currency);
}

internal sealed record RefundAcceptedResponse(Guid RefundId, MoneyOut Amount, string Status, MoneyOut RefundedTotal, MoneyOut Remaining);

/// <summary>
/// Admin refund (arch 8.5, refund contract in 9). Thin: validate the shape, call <see cref="RefundService"/>, map the outcome.
/// The amount arrives as a decimal string; every total in the answer is computed on the server (rule 1).
/// </summary>
[ApiController]
[Route("api/v1/admin/orders/{id:guid}/refund")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminRefundController(RefundService refunds) : ControllerBase
{
    private const int MaxKeyLength = 100;
    private const int MaxReasonLength = 500;

    [HttpPost("")]
    public async Task<IActionResult> Refund(
        Guid id, RefundBody body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        string key = idempotencyKey?.Trim() ?? "";
        if (key.Length is < 1 or > MaxKeyLength)
        {
            ModelState.AddModelError("Idempotency-Key", $"Send an Idempotency-Key header of 1 to {MaxKeyLength} characters.");
        }
        decimal amount = 0m;
        if (body.Amount?.Amount is null
            || !decimal.TryParse(body.Amount.Amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount) || amount <= 0m)
        {
            ModelState.AddModelError("amount.amount", "Enter an amount greater than zero as a decimal string such as \"12.50\".");
        }
        string currency = body.Amount?.Currency?.Trim() ?? "";
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
        {
            ModelState.AddModelError("amount.currency", "Enter the order's 3-letter currency.");
        }
        string reason = body.Reason?.Trim() ?? "";
        if (reason.Length is < 1 or > MaxReasonLength)
        {
            ModelState.AddModelError("reason", $"Enter a reason of 1 to {MaxReasonLength} characters.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        Guid actorId = Guid.Parse(User.FindFirst("sub")!.Value);
        RefundResult result = await refunds.RequestAsync(new RefundCommand(actorId, id, amount, currency.ToUpperInvariant(), reason, key), ct);
        switch (result)
        {
            case RefundResult.Accepted ok:
                RefundAccepted a = ok.Value;
                return Accepted(new RefundAcceptedResponse(
                    a.RefundId, MoneyOut.From(a.Amount, a.Currency), a.Status, MoneyOut.From(a.RefundedTotal, a.Currency), MoneyOut.From(a.Remaining, a.Currency)));
            case RefundResult.Invalid invalid:
                ModelState.AddModelError(invalid.Field, invalid.Message);
                return ValidationProblem(ModelState);
            case RefundResult.KeyConflict:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "This Idempotency-Key was already used for a different request.");
            case RefundResult.Conflict conflict:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "Refund not allowed", detail: conflict.Message, type: conflict.Type);
            default:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found");
        }
    }
}
