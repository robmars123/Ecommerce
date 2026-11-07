using Ecommerce.SharedKernel.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Persistence.Audit;

internal sealed class AuditLogEntry
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id).HasName("pk_audit_log");
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.ActorId).HasColumnName("actor_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id");
        builder.Property(a => a.BeforeJson).HasColumnName("before_json").HasColumnType("jsonb");
        builder.Property(a => a.AfterJson).HasColumnName("after_json").HasColumnType("jsonb");
        builder.Property(a => a.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.CreatedAt }).HasDatabaseName("ix_audit_log_entity");
    }
}

/// <summary>Adds to the scoped context only; the caller's SaveChanges (or transaction) persists it.</summary>
internal sealed class EfAuditLog(EcommerceDbContext db) : IAuditLog
{
    public void Add(AuditEntry entry) =>
        db.Set<AuditLogEntry>().Add(new AuditLogEntry
        {
            Id = Guid.CreateVersion7(entry.At),
            ActorId = entry.ActorId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            Reason = entry.Reason,
            CreatedAt = entry.At,
        });
}
