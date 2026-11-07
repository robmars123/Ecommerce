using Ecommerce.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecommerce.Persistence.Idempotency;

internal sealed class IdempotencyKeyEntry
{
    public string Scope { get; set; } = "";
    public string Key { get; set; } = "";
    public Guid? UserId { get; set; }
    public string RequestHash { get; set; } = "";
    public string? ResultRef { get; set; }
    public string? ResponseJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class IdempotencyKeyEntryConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntry> builder)
    {
        builder.ToTable("idempotency_keys", t => t.HasCheckConstraint(
            "ck_idempotency_keys_scope", "scope IN ('checkout','retry-payment','refund','cart-merge','stripe-event')"));
        builder.HasKey(e => new { e.Scope, e.Key }).HasName("pk_idempotency_keys");
        builder.Property(e => e.Scope).HasColumnName("scope").HasMaxLength(30);
        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(100);
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.RequestHash).HasColumnName("request_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ResultRef).HasColumnName("result_ref").HasMaxLength(100);
        builder.Property(e => e.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
    }
}

/// <summary>Adds to the scoped context only; the caller's transaction persists it.</summary>
internal sealed class EfIdempotencyStore(EcommerceDbContext db) : IIdempotencyStore
{
    public async Task<IdempotencyEntry?> FindAsync(string scope, string key, CancellationToken ct) =>
        await db.Set<IdempotencyKeyEntry>().AsNoTracking()
            .Where(e => e.Scope == scope && e.Key == key)
            .Select(e => new IdempotencyEntry(e.Scope, e.Key, e.UserId, e.RequestHash, e.ResultRef, e.ResponseJson, e.CreatedAt, e.ExpiresAt))
            .SingleOrDefaultAsync(ct);

    public void Add(IdempotencyEntry entry) =>
        db.Set<IdempotencyKeyEntry>().Add(new IdempotencyKeyEntry
        {
            Scope = entry.Scope,
            Key = entry.Key,
            UserId = entry.UserId,
            RequestHash = entry.RequestHash,
            ResultRef = entry.ResultRef,
            ResponseJson = entry.ResponseJson,
            CreatedAt = entry.CreatedAt,
            ExpiresAt = entry.ExpiresAt,
        });
}
