using System.Text.Json.Serialization;
using Ecommerce.Api.Health;
using Ecommerce.Api.Observability;
using Ecommerce.Api.Composition;
using Ecommerce.Api.Time;
using Ecommerce.Catalog;
using Ecommerce.Inventory;
using Ecommerce.Notifications;
using Ecommerce.SharedKernel.Time;
using Ecommerce.Persistence;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateScopes = true;
    o.ValidateOnBuild = true;
});

builder.Services.AddEcommerceSerilog(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    if (ctx.HttpContext.Items[CorrelationId.ItemKey] is string id)
    {
        ctx.ProblemDetails.Extensions["correlationId"] = id;
    }
});
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Strict request binding: an unknown member ("role", "isActive", "price" on the wrong DTO) is a 400, never
        // silently ignored. This is the mass-assignment defense (D13).
        o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    })
    .AddModuleControllers();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IConfiguration>((o, config) =>
    {
        // Configured origins only; never "*" with credentials (architecture 10.1). Empty list = no cross-origin access.
        string[] origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        string[] methods = [HttpMethods.Get, HttpMethods.Post, HttpMethods.Put];
        string[] headers = [CorrelationId.HeaderName, "Content-Type", "Authorization", RefreshTokenHeader, "Idempotency-Key", "X-SignalR-User-Agent", "X-Requested-With"];

        // Default policy (health and anything not listed below): no credentials.
        o.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods(methods)
            .WithHeaders(headers)
            .WithExposedHeaders(CorrelationId.HeaderName));

        // Auth and admin endpoints: the web refresh cookie rides on these, so credentials are allowed (D6). Applied only
        // to those routes, and only for the configured origins, so an unlisted origin never gets an ACAO header.
        // The cart is the one public-policy route that deletes (remove a line), so only /api/v1/cart also allows DELETE.
        o.AddPolicy(CartCorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods([.. methods, HttpMethods.Delete])
            .WithHeaders(headers)
            .WithExposedHeaders(CorrelationId.HeaderName));

        o.AddPolicy(CredentialedCorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods(methods)
            .WithHeaders(headers)
            .WithExposedHeaders(CorrelationId.HeaderName)
            .AllowCredentials());
    });
builder.Services.AddCors();
builder.Services.AddEcommercePersistence();
builder.Services.AddEcommerceModules(builder.Configuration);
builder.Services.AddSingleton<IClock, SystemClock>();

if (builder.Configuration.GetValue<bool>(CatalogModule.DevSeedEnabledKey))
{
    // A misconfigured non-Development host must never start with seeding on.
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            $"{CatalogModule.DevSeedEnabledKey} is true but the environment is '{builder.Environment.EnvironmentName}'. " +
            "Dev seeding is allowed only in Development.");
    }
    builder.Services.AddCatalogDevSeed();
    builder.Services.AddInventoryDevSeed();
}

builder.Services.AddHealthChecks().AddPostgresReadinessCheck();

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o =>
{
    SerilogSetup.ConfigureRequestLogging(o);
    // Which user made the request (rule: answerable from logs). The sub claim only; never a token, header or body.
    o.EnrichDiagnosticContext = (diagnostics, http) =>
    {
        if (http.User.FindFirst("sub")?.Value is { } userId)
        {
            diagnostics.Set("UserId", userId);
        }
    };
    // preserveStaticLogger: true leaves Log.Logger silent; use the DI-registered logger.
    o.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapEcommerceHealthEndpoints();
app.MapNotificationsHub();
app.MapControllers().Add(endpoint =>
{
    if (endpoint is RouteEndpointBuilder { RoutePattern.RawText: { } route } &&
        (route.StartsWith("api/v1/auth", StringComparison.OrdinalIgnoreCase) || route.StartsWith("api/v1/admin", StringComparison.OrdinalIgnoreCase)))
    {
        endpoint.Metadata.Add(new EnableCorsAttribute(CredentialedCorsPolicy));
    }
    else if (endpoint is RouteEndpointBuilder { RoutePattern.RawText: { } cartRoute } && cartRoute.StartsWith("api/v1/cart", StringComparison.OrdinalIgnoreCase))
    {
        endpoint.Metadata.Add(new EnableCorsAttribute(CartCorsPolicy));
    }
});

// Unknown routes: the authenticated-user fallback policy would otherwise turn a 404 into a 401 and leak nothing useful.
// An anonymous terminal endpoint keeps them 404 (problem+json via status code pages).
app.MapFallback(context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
}).AllowAnonymous();

app.Run();

public partial class Program
{
    private const string CredentialedCorsPolicy = "credentialed";
    private const string CartCorsPolicy = "cart";
    private const string RefreshTokenHeader = "X-Auth-Transport";
}
