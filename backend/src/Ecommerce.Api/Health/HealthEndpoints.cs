using Ecommerce.Persistence.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Ecommerce.Api.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapEcommerceHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // The authorization fallback policy requires a user on every endpoint, so probes must opt out explicitly.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthCheckTags.Ready)
        }).AllowAnonymous();
        return endpoints;
    }
}
