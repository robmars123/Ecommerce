using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Catalog.Domain;
using Ecommerce.Identity.Contracts;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api.Admin;

/// <summary>Admin only (rule 11). Thin: validate, call the service, map the outcome.</summary>
[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminProductsController(
    AdminProductService products,
    AdminProductImageService images,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator) : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        AdminCriteriaResult parsed = AdminProductListCriteria.Create(search, category, status, page, pageSize);
        if (!parsed.IsValid)
        {
            foreach ((string? field, string[]? messages) in parsed.Errors!)
            {
                foreach (string message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }
            return ValidationProblem(ModelState);
        }

        return Ok((await products.ListAsync(parsed.Criteria!, ct)).ToResponse());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Map(await products.GetAsync(id, ct), v => Ok(v.ToResponse()));

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct) =>
        Map(await products.PreviewAsync(id, ct), v => Ok(v.ToResponse()));

    [HttpPost("")]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        ValidationResult validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        CreateProductCommand command = new CreateProductCommand(
            request.Slug, request.Name, request.Description, request.CategorySlug,
            request.Variants.Select(v => new VariantInput(
                null, v.Sku, v.Name, v.Size, v.Colour, MoneyParsing.Parse(v.Price.Amount), v.Price.Currency,
                v.CompareAtPrice is null ? null : MoneyParsing.Parse(v.CompareAtPrice.Amount), v.WeightGrams, true)).ToList(),
            request.Images.Select(i => new ImageInput(i.StorageKey, i.AltText)).ToList());

        return Map(await products.CreateAsync(command, ct),
            v => Created($"/api/v1/admin/products/{v.Id}", v.ToResponse()));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        ValidationResult validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        UpdateProductCommand command = new UpdateProductCommand(
            request.Version, request.Slug, request.Name, request.Description, request.CategorySlug,
            request.Variants.Select(v => new VariantInput(
                v.Id, v.Sku, v.Name, v.Size, v.Colour, MoneyParsing.Parse(v.Price.Amount), v.Price.Currency,
                v.CompareAtPrice is null ? null : MoneyParsing.Parse(v.CompareAtPrice.Amount), v.WeightGrams, v.IsActive)).ToList(),
            request.Images.Select(i => new ImageInput(i.StorageKey, i.AltText)).ToList());

        return Map(await products.UpdateAsync(id, command, ct), v => Ok(v.ToResponse()));
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct) =>
        Map(await products.PublishAsync(id, ct), v => Ok(v.ToResponse()));

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct) =>
        Map(await products.UnpublishAsync(id, ct), v => Ok(v.ToResponse()));

    /// <summary>Stores one image and returns its key only; attaching it is the next product update (D2).</summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageFormat.MaxBytes + 1)]
    [RequestSizeLimit(ImageFormat.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(Guid id, IFormFile file, CancellationToken ct)
    {
        await using Stream content = file.OpenReadStream();
        return Map(await images.UploadAsync(id, new UploadImageCommand(content, file.Length), ct), v => Ok(v.ToResponse()));
    }

    private IActionResult Map<T>(AdminResult<T> result, Func<T, IActionResult> ok) =>
        result switch
        {
            AdminResult<T>.Ok success => ok(success.Value),
            AdminResult<T>.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found"),
            AdminResult<T>.Invalid invalid => InvalidProblem(invalid.Errors),
            AdminResult<T>.Conflict conflict => ConflictProblem(conflict.Field, conflict.Message),
            AdminResult<T>.Unavailable u => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Image storage is not configured", detail: u.Message),
            _ => throw new InvalidOperationException("Unhandled admin result."),
        };

    private ActionResult InvalidProblem(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach ((string? field, string[]? messages) in errors)
        {
            foreach (string message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }
        return ValidationProblem(ModelState);
    }

    private ActionResult ConflictProblem(string? field, string message)
    {
        ModelState.AddModelError(field ?? "", message);
        return ValidationProblem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", modelStateDictionary: ModelState);
    }
}
