using Ecommerce.Identity.Application;

namespace Ecommerce.Identity.Api;

internal sealed record UserResponse(Guid Id, string Email, string DisplayName, string Role)
{
    public static UserResponse From(UserView user) => new(user.Id, user.Email, user.DisplayName, user.Role.ToString());
}

/// <summary>RefreshToken is null when the client asked for the cookie transport (it travels in Set-Cookie instead).</summary>
internal sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string? RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserResponse User)
{
    public static TokenResponse From(SessionIssued session, bool refreshTokenInCookie) => new(
        session.AccessToken,
        session.AccessTokenExpiresAt,
        refreshTokenInCookie ? null : session.RefreshToken,
        session.RefreshTokenExpiresAt,
        UserResponse.From(session.User));
}
