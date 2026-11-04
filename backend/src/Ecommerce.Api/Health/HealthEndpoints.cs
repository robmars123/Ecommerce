using Ecommerce.Persistence.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Ecommerce.Api.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapEcommerceHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthCheckTags.Ready)
        });
        return endpoints;
    }
}
