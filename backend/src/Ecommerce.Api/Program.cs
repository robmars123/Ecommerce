using System.Text.Json.Serialization;
using Ecommerce.Api.Health;
using Ecommerce.Api.Observability;
using Ecommerce.Api.Composition;
using Ecommerce.Api.Time;
using Ecommerce.Catalog;
using Ecommerce.SharedKernel.Time;
using Ecommerce.Persistence;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddModuleControllers();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IConfiguration>((o, config) =>
    {
        // Configured origins only; never "*" with credentials (architecture 10.1). Empty list = no cross-origin access.
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        o.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods(HttpMethods.Get)
            .WithHeaders(CorrelationId.HeaderName, "Content-Type")
            .WithExposedHeaders(CorrelationId.HeaderName));
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
}

builder.Services.AddHealthChecks().AddPostgresReadinessCheck();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o =>
{
    SerilogSetup.ConfigureRequestLogging(o);
    // preserveStaticLogger: true leaves Log.Logger silent; use the DI-registered logger.
    o.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.MapEcommerceHealthEndpoints();
app.MapControllers();

app.Run();

public partial class Program;
