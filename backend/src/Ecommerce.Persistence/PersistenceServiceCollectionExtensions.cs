using Ecommerce.Persistence.Health;
using Ecommerce.Persistence.Audit;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Outbox;
using Ecommerce.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ecommerce.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddEcommercePersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((o, c) => o.ConnectionString = c.GetConnectionString("Postgres") ?? "")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "ConnectionStrings:Postgres is not configured.")
            .ValidateOnStart();

        services.AddDbContext<EcommerceDbContext>((sp, o) =>
            o.UseNpgsql(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        services.AddScoped<IIdempotencyStore, Idempotency.EfIdempotencyStore>();
        services.AddScoped<IOutbox, Outbox.EfOutbox>();
        services.AddScoped<Outbox.OutboxProcessor>();
        services.AddHostedService<Outbox.OutboxWorker>();

        return services;
    }

    public static IHealthChecksBuilder AddPostgresReadinessCheck(this IHealthChecksBuilder builder) =>
        builder.AddCheck<PostgresReadinessHealthCheck>(
            "postgres",
            HealthStatus.Unhealthy,
            tags: [HealthCheckTags.Ready],
            timeout: TimeSpan.FromSeconds(5));
}
