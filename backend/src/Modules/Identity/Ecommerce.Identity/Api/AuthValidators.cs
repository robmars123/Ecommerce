using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Identity.Api;

internal static class AuthLimits
{
    public const int MaxOpaqueTokenLength = 100;
}

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).EmailRule();
        RuleFor(r => r.Password).PasswordRule();
        RuleFor(r => r.DisplayName)
            .NotNull().WithMessage("Enter your name.")
            .Must(n => (n ?? "").Trim().Length is >= 1 and <= User.MaxDisplayNameLength)
            .WithMessage($"Name must be 1 to {User.MaxDisplayNameLength} characters.");
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).EmailRule();
        // No policy on login: an old password must still be able to try. The cap only bounds PBKDF2 input.
        RuleFor(r => r.Password)
            .NotEmpty().WithMessage("Enter your password.")
            .MaximumLength(PasswordPolicy.MaxLength).WithMessage("That password is too long.");
    }
}

internal sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() =>
        RuleFor(r => r.RefreshToken).MaximumLength(AuthLimits.MaxOpaqueTokenLength).WithMessage("That token is not valid.");
}

internal sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() =>
        RuleFor(r => r.RefreshToken).MaximumLength(AuthLimits.MaxOpaqueTokenLength).WithMessage("That token is not valid.");
}

internal sealed class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequest>
{
    public PasswordResetRequestValidator() => RuleFor(r => r.Email).EmailRule();
}

internal sealed class PasswordResetConfirmRequestValidator : AbstractValidator<PasswordResetConfirmRequest>
{
    public PasswordResetConfirmRequestValidator()
    {
        RuleFor(r => r.Token)
            .NotEmpty().WithMessage("This reset link is invalid or has expired.")
            .MaximumLength(AuthLimits.MaxOpaqueTokenLength).WithMessage("This reset link is invalid or has expired.");
        RuleFor(r => r.NewPassword).PasswordRule();
    }
}

internal static class AuthRules
{
    public static IRuleBuilderOptions<T, string> EmailRule<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Enter your email address.")
            .MaximumLength(EmailAddress.MaxLength).WithMessage($"Email must be at most {EmailAddress.MaxLength} characters.")
            .Must(e => EmailAddress.IsValid(EmailAddress.Normalize(e))).WithMessage("Enter a valid email address.");

    public static IRuleBuilderOptions<T, string> PasswordRule<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Enter a password.")
            .MinimumLength(PasswordPolicy.MinLength).WithMessage($"Password must be at least {PasswordPolicy.MinLength} characters.")
            .MaximumLength(PasswordPolicy.MaxLength).WithMessage($"Password must be at most {PasswordPolicy.MaxLength} characters.");
}

internal static class ValidationProblems
{
    /// <summary>FluentValidation result to a 400 problem. Keys are camelCase, matching the JSON the client sent.</summary>
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
