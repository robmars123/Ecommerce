using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Persistence.Projections;

/// <summary>Per projector: every outbox row at or below <see cref="LastSequence"/> is terminal for it (arch 8.8.2). Monotonic.</summary>
internal sealed class ConsumerCheckpointEntry
{
    public string Consumer { get; set; } = "";
    public long LastSequence { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>The last applied version of one aggregate for one projector; absent means 0.</summary>
internal sealed class ProjectionVersionEntry
{
    public string Projector { get; set; } = "";
    public string AggregateType { get; set; } = "";
    public Guid AggregateId { get; set; }
    public int Version { get; set; }
}

internal sealed class ConsumerCheckpointEntryConfiguration : IEntityTypeConfiguration<ConsumerCheckpointEntry>
{
    public void Configure(EntityTypeBuilder<ConsumerCheckpointEntry> builder)
    {
        builder.ToTable("consumer_checkpoints");
        builder.HasKey(e => e.Consumer).HasName("pk_consumer_checkpoints");
        builder.Property(e => e.Consumer).HasColumnName("consumer").HasMaxLength(100);
        builder.Property(e => e.LastSequence).HasColumnName("last_sequence");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

internal sealed class ProjectionVersionEntryConfiguration : IEntityTypeConfiguration<ProjectionVersionEntry>
{
    public void Configure(EntityTypeBuilder<ProjectionVersionEntry> builder)
    {
        builder.ToTable("projection_versions");
        builder.HasKey(e => new { e.Projector, e.AggregateType, e.AggregateId }).HasName("pk_projection_versions");
        builder.Property(e => e.Projector).HasColumnName("projector").HasMaxLength(100);
        builder.Property(e => e.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100);
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id");
        builder.Property(e => e.Version).HasColumnName("version");
    }
}
