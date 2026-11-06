using Ecommerce.Identity.Application;
using Microsoft.AspNetCore.Http;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation.Results;

namespace Ecommerce.Identity.Api;

/// <summary>Thin: validate, call a service, map. Every token response is Cache-Control: no-store.</summary>
[ApiController]
[Route("api/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
internal sealed class AuthController(
    RegistrationService registration,
    LoginService login,
    RefreshService refresh,
    PasswordResetService passwordReset,
    CurrentUserService currentUser,
    RefreshTokenTransport transport,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshRequest> refreshValidator,
    IValidator<LogoutRequest> logoutValidator,
    IValidator<PasswordResetRequest> resetRequestValidator,
    IValidator<PasswordResetConfirmRequest> resetConfirmValidator) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<TokenResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        ValidationResult validation = await registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        RegisterOutcome outcome = await registration.RegisterAsync(request.Email, request.Password, request.DisplayName, ct);
        return outcome switch
        {
            RegisterOutcome.Created created => Issued(created.Session, StatusCodes.Status201Created),
            _ => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account already exists",
                detail: "An account with this email already exists."),
        };
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        ValidationResult validation = await loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        LoginOutcome outcome = await login.LoginAsync(request.Email, request.Password, ct);
        return outcome switch
        {
            LoginOutcome.Success success => Issued(success.Session, StatusCodes.Status200OK),
            // One generic answer for unknown email, wrong password and locked account (D9).
            _ => Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Login failed",
                detail: "Email or password is incorrect, or the account is temporarily locked."),
        };
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest? request, CancellationToken ct)
    {
        request ??= new RefreshRequest(null);
        ValidationResult validation = await refreshValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        string? token = transport.ReadToken(Request, request.RefreshToken);
        RefreshOutcome outcome = token is null ? new RefreshOutcome.Rejected() : await refresh.RefreshAsync(token, ct);
        if (outcome is RefreshOutcome.Success success)
        {
            return Issued(success.Session, StatusCodes.Status200OK);
        }

        if (transport.UsesCookie(Request))
        {
            transport.ExpireCookie(Response);
        }
        return Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Session expired",
            detail: "Please log in again.");
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest? request, CancellationToken ct)
    {
        request ??= new LogoutRequest(null);
        ValidationResult validation = await logoutValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        string? token = transport.ReadToken(Request, request.RefreshToken);
        if (token is not null)
        {
            await refresh.LogoutAsync(token, ct);
        }
        if (transport.UsesCookie(Request))
        {
            transport.ExpireCookie(Response);
        }
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("password-reset")]
    public async Task<IActionResult> RequestPasswordReset(PasswordResetRequest request, CancellationToken ct)
    {
        ValidationResult validation = await resetRequestValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        await passwordReset.RequestAsync(request.Email, ct);
        return Accepted();
    }

    [AllowAnonymous]
    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(PasswordResetConfirmRequest request, CancellationToken ct)
    {
        ValidationResult validation = await resetConfirmValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return this.ToProblem(validation);
        }

        ResetConfirmOutcome outcome = await passwordReset.ConfirmAsync(request.Token, request.NewPassword, ct);
        return outcome is ResetConfirmOutcome.Success
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Reset link not valid",
                detail: "This reset link is invalid or has expired.");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        // Only the caller's own id (from the validated token) is ever read: no id comes from the request.
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out Guid id))
        {
            return Unauthorized();
        }
        UserView? user = await currentUser.GetAsync(id, ct);
        return user is null ? Unauthorized() : Ok(UserResponse.From(user));
    }

    private ObjectResult Issued(SessionIssued session, int status)
    {
        bool cookie = transport.UsesCookie(Request);
        if (cookie)
        {
            transport.WriteCookie(Response, session);
        }
        return StatusCode(status, TokenResponse.From(session, cookie));
    }
}
