using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Persistence.Outbox;

public static class ProcessedEventStatus
{
    public const string Retrying = "Retrying";
    public const string Done = "Done";
    public const string DeadLetter = "DeadLetter";
}

internal sealed class ProcessedEventEntry
{
    public string Consumer { get; set; } = "";
    public Guid EventId { get; set; }
    public string Status { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

internal sealed class ProcessedEventEntryConfiguration : IEntityTypeConfiguration<ProcessedEventEntry>
{
    public void Configure(EntityTypeBuilder<ProcessedEventEntry> builder)
    {
        builder.ToTable("processed_events", t => t.HasCheckConstraint(
            "ck_processed_events_status", "status IN ('Retrying','Done','DeadLetter')"));
        builder.HasKey(e => new { e.Consumer, e.EventId }).HasName("pk_processed_events");
        builder.Property(e => e.Consumer).HasColumnName("consumer").HasMaxLength(100);
        builder.Property(e => e.EventId).HasColumnName("event_id");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Attempts).HasColumnName("attempts");
        builder.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(e => e.LastError).HasColumnName("last_error").HasMaxLength(500);
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        builder.HasIndex(e => e.NextAttemptAt).HasDatabaseName("ix_processed_events_next_attempt_at").HasFilter("status <> 'Done'");
    }
}
