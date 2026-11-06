using Ecommerce.Identity.Api;
using Ecommerce.Identity.Application;
using Ecommerce.Identity.Contracts;
using Ecommerce.Identity.Domain;
using Ecommerce.Identity.Infrastructure;
using Ecommerce.Identity.Infrastructure.Bootstrap;
using Ecommerce.Identity.Infrastructure.Jwt;
using Ecommerce.Identity.Infrastructure.PasswordReset;
using Ecommerce.Identity.Infrastructure.Persistence;
using Ecommerce.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ecommerce.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDbModel, IdentityDbModel>());

        AddOptions(services);
        AddApplication(services);
        AddInfrastructure(services);
        AddAuthenticationAndAuthorization(services);
        return services;
    }

    private static void AddOptions(IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .Configure<IConfiguration>((o, c) => c.GetSection("Identity:Jwt").Bind(o))
            .Validate<IHostEnvironment>(JwtOptionsValidation.IsValid, JwtOptionsValidation.KeyMessage)
            .ValidateOnStart();

        services.AddOptions<RefreshTokenOptions>()
            .Configure<IConfiguration>((o, c) => c.GetSection("Identity:RefreshTokens").Bind(o))
            .Validate(
                o => o.SlidingLifetimeDays >= 1 && o.AbsoluteLifetimeDays >= o.SlidingLifetimeDays && !string.IsNullOrWhiteSpace(o.Cookie.Name),
                "Identity:RefreshTokens is invalid: sliding >= 1 day, absolute >= sliding, and a cookie name are required.")
            .ValidateOnStart();

        services.AddOptions<LockoutOptions>()
            .Configure<IConfiguration>((o, c) => c.GetSection("Identity:Lockout").Bind(o))
            .Validate(o => o.MaxFailedAttempts >= 1 && o.DurationMinutes >= 1, "Identity:Lockout needs at least 1 attempt and 1 minute.")
            .ValidateOnStart();

        services.AddOptions<PasswordResetOptions>()
            .Configure<IConfiguration>((o, c) => c.GetSection("Identity:PasswordReset").Bind(o))
            .Validate(o => o.TokenLifetimeMinutes >= 1, "Identity:PasswordReset:TokenLifetimeMinutes must be at least 1.")
            .Validate(
                o => string.IsNullOrWhiteSpace(o.LinkBaseUrl)
                     || (Uri.TryCreate(o.LinkBaseUrl, UriKind.Absolute, out Uri? u) && u.Scheme is "http" or "https"),
                "Identity:PasswordReset:LinkBaseUrl must be empty or an absolute http/https URL.")
            .Validate<IHostEnvironment>(
                (o, env) => string.IsNullOrWhiteSpace(o.DevPickupDirectory) || env.IsDevelopment(),
                "Identity:PasswordReset:DevPickupDirectory may only be set in Development.")
            .ValidateOnStart();

        services.AddOptions<AdminBootstrapOptions>()
            .Configure<IConfiguration>((o, c) => c.GetSection("Identity:Bootstrap").Bind(o));
    }

    private static void AddApplication(IServiceCollection services)
    {
        // Settings records are built once from the validated options.
        services.AddSingleton(sp =>
        {
            JwtOptions jwt = sp.GetRequiredService<IOptions<JwtOptions>>().Value;
            RefreshTokenOptions refresh = sp.GetRequiredService<IOptions<RefreshTokenOptions>>().Value;
            PasswordResetOptions reset = sp.GetRequiredService<IOptions<PasswordResetOptions>>().Value;
            return new TokenLifetimes(
                TimeSpan.FromMinutes(jwt.AccessTokenMinutes),
                TimeSpan.FromDays(refresh.SlidingLifetimeDays),
                TimeSpan.FromDays(refresh.AbsoluteLifetimeDays),
                TimeSpan.FromMinutes(reset.TokenLifetimeMinutes));
        });
        services.AddSingleton(sp =>
        {
            LockoutOptions lockout = sp.GetRequiredService<IOptions<LockoutOptions>>().Value;
            return new LockoutPolicy(lockout.MaxFailedAttempts, TimeSpan.FromMinutes(lockout.DurationMinutes));
        });
        services.AddSingleton(sp =>
        {
            RefreshTokenOptions.CookieOptions cookie = sp.GetRequiredService<IOptions<RefreshTokenOptions>>().Value.Cookie;
            return new RefreshCookieSettings(cookie.Name, cookie.Secure);
        });
        services.AddSingleton(sp =>
            new PasswordResetLinkSettings(sp.GetRequiredService<IOptions<PasswordResetOptions>>().Value.LinkBaseUrl));

        services.AddScoped<SessionStarter>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<LoginService>();
        services.AddScoped<RefreshService>();
        services.AddScoped<PasswordResetService>();
        services.AddScoped<CurrentUserService>();

        services.AddSingleton<RefreshTokenTransport>();
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<RefreshRequest>, RefreshRequestValidator>();
        services.AddScoped<IValidator<LogoutRequest>, LogoutRequestValidator>();
        services.AddScoped<IValidator<PasswordResetRequest>, PasswordResetRequestValidator>();
        services.AddScoped<IValidator<PasswordResetConfirmRequest>, PasswordResetConfirmRequestValidator>();
    }

    private static void AddInfrastructure(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        services.AddSingleton<IPasswordHashing, IdentityPasswordHashing>();
        services.AddSingleton<ISecureTokenGenerator, RandomTokenGenerator>();
        services.AddSingleton<ISecurityEventSink, LoggingSecurityEventSink>();
        services.AddSingleton<JwtSigningKeyProvider>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // Development with a configured pickup directory writes a file; everywhere else nothing is sent (no provider yet).
        services.AddScoped<IPasswordResetLinkSender>(sp =>
        {
            bool configured = !string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<PasswordResetOptions>>().Value.DevPickupDirectory);
            return sp.GetRequiredService<IHostEnvironment>().IsDevelopment() && configured
                ? ActivatorUtilities.CreateInstance<DevPickupDirectoryLinkSender>(sp)
                : ActivatorUtilities.CreateInstance<NoProviderLinkSender>(sp);
        });

        services.AddHostedService<AdminBootstrapHostedService>();
    }

    private static void AddAuthenticationAndAuthorization(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>());

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Admin, p => p.RequireRole(IdentityRoles.Admin))
            .AddPolicy(AuthorizationPolicies.AdminOrSupport, p => p.RequireRole(IdentityRoles.Admin, IdentityRoles.Support))
            // Secure by default: an endpoint that declares nothing needs a signed-in user. Public endpoints opt out
            // explicitly with [AllowAnonymous] / .AllowAnonymous(), and a test lists every one that does.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }
}

internal sealed class RefreshTokenOptions
{
    public int SlidingLifetimeDays { get; set; } = 14;
    public int AbsoluteLifetimeDays { get; set; } = 30;
    public CookieOptions Cookie { get; set; } = new();

    internal sealed class CookieOptions
    {
        public string Name { get; set; } = "ecommerce_rt";
        public bool Secure { get; set; } = true;
    }
}

internal sealed class LockoutOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int DurationMinutes { get; set; } = 5;
}
