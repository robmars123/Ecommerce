using System.Security.Claims;
using Ecommerce.Cart.Application;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Cart.Api;

/// <summary>
/// Any signed-in user owns exactly their own cart: the owner is the token's <c>sub</c>, and no route carries an id.
/// Thin: validate, call the service once, map the outcome.
/// </summary>
internal static class CartEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1").RequireAuthorization();
        group.MapGet("cart", Get);
        group.MapPost("cart/items", Add);
        group.MapPut("cart/items/{variantId:guid}", Set);
        group.MapDelete("cart/items/{variantId:guid}", Remove);
        group.MapPost("cart/merge", Merge);
    }

    private static async Task<Ok<CartResponse>> Get(
        ClaimsPrincipal user,
        [FromServices] CartService cart,
        CancellationToken ct) =>
        TypedResults.Ok((await cart.GetAsync(UserId(user), ct)).ToResponse());

    private static async Task<Results<Ok<CartResponse>, ValidationProblem, ProblemHttpResult>> Add(
        [FromBody] AddCartItemRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<AddCartItemRequest> validator,
        [FromServices] CartService cart,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(ToErrors(validation));
        }

        return ToResult(await cart.AddAsync(UserId(user), request.VariantId!.Value, request.Quantity!.Value, ct));
    }

    private static async Task<Results<Ok<CartResponse>, ValidationProblem, ProblemHttpResult>> Set(
        Guid variantId,
        [FromBody] SetCartItemRequest request,
        ClaimsPrincipal user,
        [FromServices] IValidator<SetCartItemRequest> validator,
        [FromServices] CartService cart,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(ToErrors(validation));
        }

        return ToResult(await cart.SetAsync(UserId(user), variantId, request.Quantity!.Value, ct));
    }

    private static async Task<NoContent> Remove(
        Guid variantId,
        ClaimsPrincipal user,
        [FromServices] CartService cart,
        CancellationToken ct)
    {
        await cart.RemoveAsync(UserId(user), variantId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<MergeResponse>, ValidationProblem, ProblemHttpResult>> Merge(
        [FromBody] MergeCartRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ClaimsPrincipal user,
        [FromServices] IValidator<MergeCartRequest> validator,
        [FromServices] CartMergeService merge,
        CancellationToken ct)
    {
        if (!MergeCartRequestValidator.IsValidKey(idempotencyKey))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = [$"Send an Idempotency-Key header of 1 to {MergeCartRequestValidator.MaxKeyLength} characters."],
            });
        }
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(ToErrors(validation));
        }

        List<MergeLine> lines = request.Lines!.Select(l => new MergeLine(l.VariantId!.Value, l.Quantity!.Value)).ToList();
        MergeReply reply = await merge.MergeAsync(UserId(user), idempotencyKey!.Trim(), lines, ct);
        return reply.Status == MergeStatus.Ok
            ? TypedResults.Ok(reply.Result!.ToResponse())
            : TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "This Idempotency-Key was already used for a different request.");
    }

    // RequireAuthorization() guarantees an authenticated user, so the token has a valid sub.
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirst("sub")!.Value);

    private static Results<Ok<CartResponse>, ValidationProblem, ProblemHttpResult> ToResult(CartWriteResult result) =>
        result.Outcome switch
        {
            CartOutcome.Ok => TypedResults.Ok(result.Cart!.ToResponse()),
            CartOutcome.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Item not in cart"),
            CartOutcome.Insufficient => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Not enough stock",
                detail: $"Only {result.Available} available.",
                extensions: new Dictionary<string, object?> { ["available"] = result.Available }),
            _ => TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [result.Field ?? ""] = [result.Message ?? "Invalid request."],
            }),
        };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(e => e.PropertyName.Length == 0 ? "" : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
