using System.Globalization;
using System.Text.RegularExpressions;
using Ecommerce.Catalog.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Catalog.Api.Admin;

internal static partial class MoneyParsing
{
    [GeneratedRegex(@"^\d{1,15}(\.\d{1,4})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"^[A-Z]{3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyPattern();

    public static bool IsValidAmount(string? amount) => amount is not null && AmountPattern().IsMatch(amount);

    public static bool IsValidCurrency(string? currency) => currency is not null && CurrencyPattern().IsMatch(currency);

    /// <summary>The exact decimal the admin typed. Call only after <see cref="IsValidAmount"/>.</summary>
    public static decimal Parse(string amount) =>
        decimal.Parse(amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}

internal sealed class MoneyRequestValidator : AbstractValidator<MoneyRequest>
{
    public MoneyRequestValidator()
    {
        RuleFor(m => m.Amount)
            .Must(MoneyParsing.IsValidAmount)
            .WithMessage("Enter a non-negative amount with at most 4 decimal places, like 49.00.");
        RuleFor(m => m.Currency)
            .Must(MoneyParsing.IsValidCurrency)
            .WithMessage("Currency must be a 3-letter upper-case code, like USD.");
    }
}

internal abstract class VariantRequestValidator<T> : AbstractValidator<T> where T : IVariantFields
{
    protected VariantRequestValidator()
    {
        MoneyRequestValidator money = new MoneyRequestValidator();
        RuleFor(v => v.Sku)
            .NotEmpty().WithMessage("Enter a SKU.")
            .MaximumLength(Product.MaxSkuLength).WithMessage($"SKU must be at most {Product.MaxSkuLength} characters.")
            .Matches(@"^[A-Z0-9-]+\z").WithMessage("SKU may contain only A-Z, 0-9 and hyphen.");
        RuleFor(v => v.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Length <= Product.MaxVariantNameLength)
            .WithMessage($"Variant name must be 1 to {Product.MaxVariantNameLength} characters.");
        RuleFor(v => v.Size)
            .MaximumLength(Product.MaxVariantOptionLength).WithMessage($"Size must be at most {Product.MaxVariantOptionLength} characters.");
        RuleFor(v => v.Colour)
            .MaximumLength(Product.MaxVariantOptionLength).WithMessage($"Colour must be at most {Product.MaxVariantOptionLength} characters.");
        RuleFor(v => v.Price).NotNull().SetValidator(money);
        RuleFor(v => v.CompareAtPrice!).SetValidator(money).When(v => v.CompareAtPrice is not null);
        RuleFor(v => v.CompareAtPrice)
            .Must((v, compareAt) => compareAt is null || v.Price is null || compareAt.Currency == v.Price.Currency)
            .WithMessage("Compare-at price must use the same currency as the price.");
        RuleFor(v => v.WeightGrams).InclusiveBetween(0, 1_000_000).WithMessage("Weight must be between 0 and 1,000,000 grams.");
    }
}

internal sealed class CreateVariantRequestValidator : VariantRequestValidator<CreateVariantRequest>;

internal sealed class UpdateVariantRequestValidator : VariantRequestValidator<UpdateVariantRequest>;

internal sealed class ImageRequestValidator : AbstractValidator<ImageRequest>
{
    public ImageRequestValidator()
    {
        RuleFor(i => i.StorageKey)
            .Must(StorageKey.IsValid)
            .WithMessage("Image key must be a relative storage key (no '..', '\\', ':' or leading '/'), up to 512 characters.");
        RuleFor(i => i.AltText)
            .NotNull()
            .MaximumLength(Product.MaxImageAltTextLength).WithMessage($"Alt text must be at most {Product.MaxImageAltTextLength} characters.");
    }
}

internal static class ProductFieldRules
{
    public static void AddDetailRules<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> slug,
        System.Linq.Expressions.Expression<Func<T, string>> name,
        System.Linq.Expressions.Expression<Func<T, string>> description,
        System.Linq.Expressions.Expression<Func<T, string>> categorySlug)
    {
        validator.RuleFor(slug).Must(Slug.IsValid)
            .WithMessage("Slug must be lower-case letters, digits and single hyphens, up to 100 characters.");
        validator.RuleFor(name).Must(n => !string.IsNullOrWhiteSpace(n) && n.Length <= Product.MaxNameLength)
            .WithMessage($"Name must be 1 to {Product.MaxNameLength} characters.");
        validator.RuleFor(description).NotNull()
            .MaximumLength(Product.MaxDescriptionLength).WithMessage($"Description must be at most {Product.MaxDescriptionLength} characters.");
        validator.RuleFor(categorySlug).Must(Slug.IsValid).WithMessage("Choose a category.");
    }
}

internal sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        ProductFieldRules.AddDetailRules(this, r => r.Slug, r => r.Name, r => r.Description, r => r.CategorySlug);
        RuleFor(r => r.Variants).NotNull()
            .Must(v => v.Count <= Product.MaxVariants).WithMessage($"A product can have at most {Product.MaxVariants} variants.");
        RuleForEach(r => r.Variants).SetValidator(new CreateVariantRequestValidator());
        RuleFor(r => r.Images).NotNull()
            .Must(i => i.Count <= Product.MaxImages).WithMessage($"A product can have at most {Product.MaxImages} images.");
        RuleForEach(r => r.Images).SetValidator(new ImageRequestValidator());
    }
}

internal sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        ProductFieldRules.AddDetailRules(this, r => r.Slug, r => r.Name, r => r.Description, r => r.CategorySlug);
        RuleFor(r => r.Variants).NotNull()
            .Must(v => v.Count <= Product.MaxVariants).WithMessage($"A product can have at most {Product.MaxVariants} variants.");
        RuleForEach(r => r.Variants).SetValidator(new UpdateVariantRequestValidator());
        RuleFor(r => r.Images).NotNull()
            .Must(i => i.Count <= Product.MaxImages).WithMessage($"A product can have at most {Product.MaxImages} images.");
        RuleForEach(r => r.Images).SetValidator(new ImageRequestValidator());
    }
}

internal sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        CategoryFieldRules.Add(this, r => r.Slug, r => r.Name);
        RuleFor(r => r.ParentSlug).Must(s => s is null || s.Length == 0 || Slug.IsValid(s))
            .WithMessage("Parent must be a valid category slug.");
    }
}

internal sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator() => CategoryFieldRules.Add(this, r => r.Slug, r => r.Name);
}

internal static class CategoryFieldRules
{
    public static void Add<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, string>> slug, System.Linq.Expressions.Expression<Func<T, string>> name)
    {
        validator.RuleFor(slug).Must(Slug.IsValid)
            .WithMessage("Slug must be lower-case letters, digits and single hyphens, up to 100 characters.");
        validator.RuleFor(name).Must(n => !string.IsNullOrWhiteSpace(n) && n.Length <= Category.MaxNameLength)
            .WithMessage($"Name must be 1 to {Category.MaxNameLength} characters.");
    }
}

internal static class AdminValidationProblems
{
    /// <summary>FluentValidation result to a 400 problem. Keys are camelCase paths, like variants[0].price.amount.</summary>
    public static ActionResult ToProblem(this ControllerBase controller, FluentValidation.Results.ValidationResult result)
    {
        foreach (ValidationFailure? error in result.Errors)
        {
            controller.ModelState.AddModelError(CamelCase(error.PropertyName), error.ErrorMessage);
        }
        return controller.ValidationProblem(controller.ModelState);
    }

    private static string CamelCase(string path) =>
        string.Join('.', path.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));
}
