using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Identity.Contracts;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api.Admin;

[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
internal sealed class AdminCategoriesController(
    AdminCategoryService categories,
    IValidator<CreateCategoryRequest> validator) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        AdminResult<CategoryView> result = await categories.CreateAsync(new CreateCategoryCommand(request.Slug, request.Name, request.ParentSlug), ct);
        switch (result)
        {
            case AdminResult<CategoryView>.Ok ok:
                return StatusCode(StatusCodes.Status201Created, ok.Value.ToResponse());
            case AdminResult<CategoryView>.Invalid invalid:
                foreach ((string? field, string[]? messages) in invalid.Errors)
                {
                    foreach (string message in messages)
                    {
                        ModelState.AddModelError(field, message);
                    }
                }
                return ValidationProblem(ModelState);
            case AdminResult<CategoryView>.Conflict conflict:
                ModelState.AddModelError(conflict.Field ?? "", conflict.Message);
                return ValidationProblem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", modelStateDictionary: ModelState);
            default:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Category not found");
        }
    }
}
