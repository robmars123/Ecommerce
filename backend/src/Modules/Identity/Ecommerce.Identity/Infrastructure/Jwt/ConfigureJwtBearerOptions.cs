using Ecommerce.SharedKernel.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Identity.Infrastructure.Jwt;

/// <summary>
/// D4. Pins the algorithm (closes alg:none and algorithm confusion), requires a signature and an expiry, checks issuer,
/// audience and lifetime. Lifetime is judged by the injected clock so tests with a fake clock behave like production.
/// </summary>
internal sealed class ConfigureJwtBearerOptions(JwtSigningKeyProvider keys, IOptions<JwtOptions> jwt, IClock clock)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        JwtOptions settings = jwt.Value;
        options.MapInboundClaims = false;
        options.SaveToken = false;
        // SignalR clients cannot set headers on a WebSocket, so a hub connection sends the token as access_token. Honoured only on
        // /hubs paths (Slice 6 D16); every other route still needs the Authorization header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs") && context.Request.Query.TryGetValue("access_token", out Microsoft.Extensions.Primitives.StringValues token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keys.Key,
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                DateTimeOffset now = clock.UtcNow;
                return (notBefore is null || notBefore.Value <= now.UtcDateTime + ClockSkew)
                       && expires is not null && expires.Value > now.UtcDateTime - ClockSkew;
            },
            ClockSkew = ClockSkew,
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);
}
