using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Persistence;

public sealed class EcommerceDbContext : DbContext
{
    private readonly IReadOnlyList<IModuleDbModel> _modules;

    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options, IEnumerable<IModuleDbModel> modules)
        : base(options)
    {
        _modules = modules.ToList();
        if (_modules.Count == 0)
        {
            throw new InvalidOperationException("No module models are registered; the EF model would be empty.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (IModuleDbModel module in _modules)
        {
            module.Configure(modelBuilder);
        }

        // Platform table shared by modules (D5): owned by Persistence, written through SharedKernel IAuditLog.
        modelBuilder.ApplyConfiguration(new Audit.AuditLogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Idempotency.IdempotencyKeyEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Outbox.OutboxEventEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Outbox.ProcessedEventEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Projections.ConsumerCheckpointEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Projections.ProjectionVersionEntryConfiguration());
    }
}
