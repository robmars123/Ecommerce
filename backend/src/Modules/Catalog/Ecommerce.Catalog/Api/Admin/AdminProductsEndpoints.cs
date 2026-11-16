using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.Identity.Contracts;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Catalog.Api.Admin;

/// <summary>Admin only (rule 11). Thin: validate, call the service once, map the outcome.</summary>
internal static class AdminProductsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/admin").RequireAuthorization(AuthorizationPolicies.Admin);
        group.MapGet("products", List);
        group.MapGet("products/{id:guid}", Get);
        group.MapGet("products/{id:guid}/preview", Preview);
        group.MapPost("products", Create);
        group.MapPut("products/{id:guid}", Update);
        group.MapPost("products/{id:guid}/publish", Publish);
        group.MapPost("products/{id:guid}/unpublish", Unpublish);
        group.MapPost("products/{id:guid}/images", Upload)
            .WithFormOptions(multipartBodyLengthLimit: ImageFormat.MaxBytes + 1)
            .WithMetadata(new RequestSizeLimitAttribute(ImageFormat.MaxBytes + 64 * 1024))
            .DisableAntiforgery();
    }

    private static async Task<Results<Ok<AdminProductListResponse>, ValidationProblem>> List(
        [AsParameters] AdminProductListQuery query,
        [FromServices] AdminProductService products,
        CancellationToken ct)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        int? pageNumber = QueryValues.ReadInt(query.Page, "page", errors);
        int? pageSizeNumber = QueryValues.ReadInt(query.PageSize, "pageSize", errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        AdminCriteriaResult parsed = AdminProductListCriteria.Create(
            query.Search?.FirstOrDefault(), query.Category?.FirstOrDefault(), query.Status?.FirstOrDefault(), pageNumber, pageSizeNumber);
        if (!parsed.IsValid)
        {
            return TypedResults.ValidationProblem(parsed.Errors!);
        }

        return TypedResults.Ok((await products.ListAsync(parsed.Criteria!, ct)).ToResponse());
    }

    private static async Task<Results<Ok<AdminProductResponse>, ValidationProblem, ProblemHttpResult>> Get(
        Guid id,
        [FromServices] AdminProductService products,
        CancellationToken ct) =>
        ToResult(await products.GetAsync(id, ct), view => TypedResults.Ok(view.ToResponse()));

    private static async Task<Results<Ok<AdminPreviewResponse>, ValidationProblem, ProblemHttpResult>> Preview(
        Guid id,
        [FromServices] AdminProductService products,
        CancellationToken ct) =>
        ToResult(await products.PreviewAsync(id, ct), view => TypedResults.Ok(view.ToResponse()));

    private static async Task<Results<Created<AdminProductResponse>, ValidationProblem, ProblemHttpResult>> Create(
        [FromBody] CreateProductRequest request,
        [FromServices] IValidator<CreateProductRequest> validator,
        [FromServices] AdminProductService products,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        CreateProductCommand command = new CreateProductCommand(
            request.Slug, request.Name, request.Description, request.CategorySlug,
            request.Variants.Select(v => new VariantInput(
                null, v.Sku, v.Name, v.Size, v.Colour, MoneyParsing.Parse(v.Price.Amount), v.Price.Currency,
                v.CompareAtPrice is null ? null : MoneyParsing.Parse(v.CompareAtPrice.Amount), v.WeightGrams, true)).ToList(),
            request.Images.Select(i => new ImageInput(i.StorageKey, i.AltText)).ToList());

        return ToResult(await products.CreateAsync(command, ct),
            view => TypedResults.Created($"/api/v1/admin/products/{view.Id}", view.ToResponse()));
    }

    private static async Task<Results<Ok<AdminProductResponse>, ValidationProblem, ProblemHttpResult>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        [FromServices] IValidator<UpdateProductRequest> validator,
        [FromServices] AdminProductService products,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        UpdateProductCommand command = new UpdateProductCommand(
            request.Version, request.Slug, request.Name, request.Description, request.CategorySlug,
            request.Variants.Select(v => new VariantInput(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, MoneyParsing.Parse(v.Price.Amount), v.Price.Currency,
                v.CompareAtPrice is null ? null : MoneyParsing.Parse(v.CompareAtPrice.Amount), v.WeightGrams, v.IsActive)).ToList(),
            request.Images.Select(i => new ImageInput(i.StorageKey, i.AltText)).ToList());

        return ToResult(await products.UpdateAsync(id, command, ct), view => TypedResults.Ok(view.ToResponse()));
    }

    private static async Task<Results<Ok<AdminProductResponse>, ValidationProblem, ProblemHttpResult>> Publish(
        Guid id,
        [FromServices] AdminProductService products,
        CancellationToken ct) =>
        ToResult(await products.PublishAsync(id, ct), view => TypedResults.Ok(view.ToResponse()));

    private static async Task<Results<Ok<AdminProductResponse>, ValidationProblem, ProblemHttpResult>> Unpublish(
        Guid id,
        [FromServices] AdminProductService products,
        CancellationToken ct) =>
        ToResult(await products.UnpublishAsync(id, ct), view => TypedResults.Ok(view.ToResponse()));

    /// <summary>Stores one image and returns its key and URL; attaching it is the next product update (D2).</summary>
    private static async Task<Results<Ok<UploadedImageResponse>, ValidationProblem, ProblemHttpResult>> Upload(
        Guid id,
        IFormFile? file,
        [FromServices] AdminProductImageService images,
        CancellationToken ct)
    {
        if (file is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["The file field is required."] });
        }

        await using Stream content = file.OpenReadStream();
        return ToResult(await images.UploadAsync(id, new UploadImageCommand(content, file.Length), ct), view => TypedResults.Ok(view.ToResponse()));
    }

    private static Results<TOk, ValidationProblem, ProblemHttpResult> ToResult<T, TOk>(AdminResult<T> result, Func<T, TOk> onOk)
        where TOk : IResult =>
        result switch
        {
            AdminResult<T>.Ok ok => onOk(ok.Value),
            AdminResult<T>.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found"),
            AdminResult<T>.Invalid invalid => TypedResults.ValidationProblem(invalid.Errors),
            AdminResult<T>.Conflict conflict => TypedResults.Problem(
                new HttpValidationProblemDetails(new Dictionary<string, string[]> { [conflict.Field ?? ""] = [conflict.Message] })
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                }),
            _ => throw new InvalidOperationException("Unhandled admin result."),
        };
}
