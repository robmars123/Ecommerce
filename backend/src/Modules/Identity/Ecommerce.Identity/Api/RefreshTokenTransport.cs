using Ecommerce.Identity.Application;
using Microsoft.AspNetCore.Http;

namespace Ecommerce.Identity.Api;

/// <summary>
/// D6. Native clients exchange the refresh token in the JSON body (stored in SecureStore). Web clients opt in to an
/// httpOnly cookie with the header <c>X-Auth-Transport: cookie</c>. With that header the server never puts the token in
/// the body and reads it only from the cookie; without it the cookie is ignored. Requiring a custom header forces a CORS
/// preflight on cross-origin calls, so another origin cannot ride the cookie (CSRF).
/// </summary>
internal sealed class RefreshTokenTransport(RefreshCookieSettings settings)
{
    public const string HeaderName = "X-Auth-Transport";
    private const string CookieMode = "cookie";
    private const string CookiePath = "/api/v1/auth";

    public bool UsesCookie(HttpRequest request) =>
        string.Equals(request.Headers[HeaderName].ToString(), CookieMode, StringComparison.OrdinalIgnoreCase);

    public string? ReadToken(HttpRequest request, string? bodyToken)
    {
        string? token = UsesCookie(request) ? request.Cookies[settings.Name] : bodyToken;
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public void WriteCookie(HttpResponse response, SessionIssued session) =>
        response.Cookies.Append(settings.Name, session.RefreshToken, Options(session.RefreshTokenExpiresAt));

    public void ExpireCookie(HttpResponse response) =>
        response.Cookies.Append(settings.Name, "", Options(DateTimeOffset.UnixEpoch));

    private CookieOptions Options(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = settings.Secure,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
    };
}
