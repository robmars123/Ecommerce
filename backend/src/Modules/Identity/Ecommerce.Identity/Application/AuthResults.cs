using Ecommerce.Identity.Domain;

namespace Ecommerce.Identity.Application;

internal sealed record UserView(Guid Id, string Email, string DisplayName, UserRole Role)
{
    public static UserView From(User user) => new(user.Id, user.Email, user.DisplayName, user.Role);
}

internal sealed record SessionIssued(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserView User);

internal abstract record RegisterOutcome
{
    private RegisterOutcome() { }

    public sealed record Created(SessionIssued Session) : RegisterOutcome;

    public sealed record DuplicateEmail : RegisterOutcome;
}

internal abstract record LoginOutcome
{
    private LoginOutcome() { }

    public sealed record Success(SessionIssued Session) : LoginOutcome;

    public sealed record InvalidCredentials : LoginOutcome;
}

internal abstract record RefreshOutcome
{
    private RefreshOutcome() { }

    public sealed record Success(SessionIssued Session) : RefreshOutcome;

    public sealed record Rejected : RefreshOutcome;
}

internal abstract record ResetConfirmOutcome
{
    private ResetConfirmOutcome() { }

    public sealed record Success : ResetConfirmOutcome;

    public sealed record InvalidToken : ResetConfirmOutcome;
}
