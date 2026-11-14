using System.Security.Claims;
using Ecommerce.Identity.Application;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Ecommerce.Identity.Api;

/// <summary>Thin: validate, call a service, map. Every response from these handlers is Cache-Control: no-store.</summary>
internal static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/v1/auth").AddEndpointFilter(NoStore);
        group.MapPost("register", Register).AllowAnonymous();
        group.MapPost("login", Login).AllowAnonymous();
        group.MapPost("refresh", Refresh).AllowAnonymous();
        group.MapPost("logout", Logout).AllowAnonymous();
        group.MapPost("password-reset", RequestPasswordReset).AllowAnonymous();
        group.MapPost("password-reset/confirm", ConfirmPasswordReset).AllowAnonymous();
        group.MapGet("me", Me).RequireAuthorization();
    }

    // Headers are set before the handler runs, so no early return can skip them.
    private static async ValueTask<object?> NoStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store,no-cache";
        context.HttpContext.Response.Headers.Pragma = "no-cache";
        return await next(context);
    }

    private static async Task<Results<Created<TokenResponse>, ValidationProblem, ProblemHttpResult>> Register(
        [FromBody] RegisterRequest request,
        HttpContext http,
        [FromServices] IValidator<RegisterRequest> validator,
        [FromServices] RegistrationService registration,
        [FromServices] RefreshTokenTransport transport,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        RegisterOutcome outcome = await registration.RegisterAsync(request.Email, request.Password, request.DisplayName, ct);
        return outcome switch
        {
            RegisterOutcome.Created created => TypedResults.Created((string?)null, Issue(http, transport, created.Session)),
            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account already exists",
                detail: "An account with this email already exists."),
        };
    }

    private static async Task<Results<Ok<TokenResponse>, ValidationProblem, ProblemHttpResult>> Login(
        [FromBody] LoginRequest request,
        HttpContext http,
        [FromServices] IValidator<LoginRequest> validator,
        [FromServices] LoginService login,
        [FromServices] RefreshTokenTransport transport,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        LoginOutcome outcome = await login.LoginAsync(request.Email, request.Password, ct);
        return outcome switch
        {
            LoginOutcome.Success success => TypedResults.Ok(Issue(http, transport, success.Session)),
            // One generic answer for unknown email, wrong password and locked account (D9).
            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Login failed",
                detail: "Email or password is incorrect, or the account is temporarily locked."),
        };
    }

    private static async Task<Results<Ok<TokenResponse>, ValidationProblem, ProblemHttpResult>> Refresh(
        [FromBody] RefreshRequest? request,
        HttpContext http,
        [FromServices] IValidator<RefreshRequest> validator,
        [FromServices] RefreshService refresh,
        [FromServices] RefreshTokenTransport transport,
        CancellationToken ct)
    {
        request ??= new RefreshRequest(null);
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        string? token = transport.ReadToken(http.Request, request.RefreshToken);
        RefreshOutcome outcome = token is null ? new RefreshOutcome.Rejected() : await refresh.RefreshAsync(token, ct);
        if (outcome is RefreshOutcome.Success success)
        {
            return TypedResults.Ok(Issue(http, transport, success.Session));
        }

        if (transport.UsesCookie(http.Request))
        {
            transport.ExpireCookie(http.Response);
        }
        return TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Session expired",
            detail: "Please log in again.");
    }

    private static async Task<Results<NoContent, ValidationProblem>> Logout(
        [FromBody] LogoutRequest? request,
        HttpContext http,
        [FromServices] IValidator<LogoutRequest> validator,
        [FromServices] RefreshService refresh,
        [FromServices] RefreshTokenTransport transport,
        CancellationToken ct)
    {
        request ??= new LogoutRequest(null);
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        string? token = transport.ReadToken(http.Request, request.RefreshToken);
        if (token is not null)
        {
            await refresh.LogoutAsync(token, ct);
        }
        if (transport.UsesCookie(http.Request))
        {
            transport.ExpireCookie(http.Response);
        }
        return TypedResults.NoContent();
    }

    private static async Task<Results<Accepted, ValidationProblem>> RequestPasswordReset(
        [FromBody] PasswordResetRequest request,
        [FromServices] IValidator<PasswordResetRequest> validator,
        [FromServices] PasswordResetService passwordReset,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        await passwordReset.RequestAsync(request.Email, ct);
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> ConfirmPasswordReset(
        [FromBody] PasswordResetConfirmRequest request,
        [FromServices] IValidator<PasswordResetConfirmRequest> validator,
        [FromServices] PasswordResetService passwordReset,
        CancellationToken ct)
    {
        ValidationResult validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToErrors());
        }

        ResetConfirmOutcome outcome = await passwordReset.ConfirmAsync(request.Token, request.NewPassword, ct);
        return outcome is ResetConfirmOutcome.Success
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Reset link not valid",
                detail: "This reset link is invalid or has expired.");
    }

    private static async Task<Results<Ok<UserResponse>, UnauthorizedHttpResult>> Me(
        ClaimsPrincipal user,
        [FromServices] CurrentUserService currentUser,
        CancellationToken ct)
    {
        // Only the caller's own id (from the validated token) is ever read: no id comes from the request.
        if (!Guid.TryParse(user.FindFirst("sub")?.Value, out Guid id))
        {
            return TypedResults.Unauthorized();
        }
        UserView? view = await currentUser.GetAsync(id, ct);
        return view is null ? TypedResults.Unauthorized() : TypedResults.Ok(UserResponse.From(view));
    }

    /// <summary>Writes the refresh cookie in cookie mode and builds the token response (no refresh token in the body then).</summary>
    private static TokenResponse Issue(HttpContext http, RefreshTokenTransport transport, SessionIssued session)
    {
        bool cookie = transport.UsesCookie(http.Request);
        if (cookie)
        {
            transport.WriteCookie(http.Response, session);
        }
        return TokenResponse.From(session, cookie);
    }
}
