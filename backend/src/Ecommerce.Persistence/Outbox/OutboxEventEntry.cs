using Ecommerce.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Persistence.Outbox;

internal sealed class OutboxEventEntry
{
    public long Sequence { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public string AggregateType { get; set; } = "";
    public Guid AggregateId { get; set; }
    public int AggregateVersion { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class OutboxEventEntryConfiguration : IEntityTypeConfiguration<OutboxEventEntry>
{
    public void Configure(EntityTypeBuilder<OutboxEventEntry> builder)
    {
        builder.ToTable("outbox_events");
        builder.HasKey(e => e.Sequence).HasName("pk_outbox_events");
        builder.Property(e => e.Sequence).HasColumnName("sequence").UseIdentityAlwaysColumn();
        builder.Property(e => e.EventId).HasColumnName("event_id");
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id");
        builder.Property(e => e.AggregateVersion).HasColumnName("aggregate_version");
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        builder.HasIndex(e => e.EventId).IsUnique().HasDatabaseName("ux_outbox_events_event_id");
    }
}

/// <summary>Adds to the scoped context only; the caller's SaveChanges (or transaction) persists it.</summary>
internal sealed class EfOutbox(EcommerceDbContext db) : IOutbox
{
    public void Add(OutboxMessage message) =>
        db.Set<OutboxEventEntry>().Add(new OutboxEventEntry
        {
            EventId = message.EventId,
            EventType = message.EventType,
            AggregateType = message.AggregateType,
            AggregateId = message.AggregateId,
            AggregateVersion = message.AggregateVersion,
            Payload = message.PayloadJson,
            OccurredAt = message.OccurredAt,
        });
}
