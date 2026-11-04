using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Ecommerce.Api.Observability;

public static class SerilogSetup
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static IServiceCollection AddEcommerceSerilog(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) =>
        services.AddSerilog((sp, lc) =>
        {
            lc.ReadFrom.Configuration(configuration)
                .ReadFrom.Services(sp)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Ecommerce.Api");

            if (environment.IsDevelopment())
            {
                lc.WriteTo.Console(outputTemplate: DevelopmentTemplate);
            }
            else
            {
                lc.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        }, preserveStaticLogger: true);

    public static void ConfigureRequestLogging(RequestLoggingOptions options)
    {
        options.GetLevel = (httpContext, _, ex) =>
        {
            if (ex is not null || httpContext.Response.StatusCode >= 500)
            {
                return LogEventLevel.Error;
            }

            if (httpContext.Request.Path.StartsWithSegments("/health") && httpContext.Response.StatusCode < 400)
            {
                return LogEventLevel.Debug;
            }

            return LogEventLevel.Information;
        };
    }
}
