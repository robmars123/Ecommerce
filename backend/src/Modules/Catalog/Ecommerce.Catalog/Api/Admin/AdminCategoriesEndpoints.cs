using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Identity.Contracts;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Catalog.Api.Admin;

/// <summary>Admin only. Thin: validate the body, call the service once, map the outcome.</summary>
internal static class AdminCategoriesEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapPost("categories", Create);
        group.MapPut("categories/{slug}", Update);
        group.MapDelete("categories/{slug}", Delete);
    }

    private static async Task<Results<Created<CategoryResponse>, ValidationProblem, ProblemHttpResult>> Create(
        [FromBody] CreateCategoryRequest request,
        [FromServices] IValidator<CreateCategoryRequest> validator,
        [FromServices] AdminCategoryService categories,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        AdminResult<CategoryView> result = await categories.CreateAsync(new CreateCategoryCommand(request.Slug, request.Name, request.ParentSlug), ct);
        return ToResult(result, view => TypedResults.Created((string?)null, view.ToResponse()));
    }

    private static async Task<Results<Ok<CategoryResponse>, ValidationProblem, ProblemHttpResult>> Update(
        string slug,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] IValidator<UpdateCategoryRequest> validator,
        [FromServices] AdminCategoryService categories,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        AdminResult<CategoryView> result = await categories.UpdateAsync(new UpdateCategoryCommand(slug, request.Slug, request.Name), ct);
        return ToResult(result, view => TypedResults.Ok(view.ToResponse()));
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> Delete(
        string slug,
        [FromServices] AdminCategoryService categories,
        CancellationToken ct)
    {
        AdminResult<CategoryView> result = await categories.DeleteAsync(slug, ct);
        return ToResult(result, _ => TypedResults.NoContent());
    }

    private static Results<TOk, ValidationProblem, ProblemHttpResult> ToResult<TOk>(AdminResult<CategoryView> result, Func<CategoryView, TOk> onOk)
        where TOk : IResult =>
        result switch
        {
            AdminResult<CategoryView>.Ok ok => onOk(ok.Value),
            AdminResult<CategoryView>.Invalid invalid => TypedResults.ValidationProblem(invalid.Errors),
            AdminResult<CategoryView>.Conflict conflict => TypedResults.Problem(
                new HttpValidationProblemDetails(new Dictionary<string, string[]> { [conflict.Field ?? ""] = [conflict.Message] })
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                }),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Category not found"),
        };
}
