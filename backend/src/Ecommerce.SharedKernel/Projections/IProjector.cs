using Ecommerce.SharedKernel.Outbox;

namespace Ecommerce.SharedKernel.Projections;

/// <summary>
/// A read-model builder over the outbox (arch 8.8). The runtime in Persistence calls <see cref="ApplyAsync"/> inside the one
/// transaction that also marks the event done and moves the version, so a duplicate is a no-op. Apply writes through the
/// scoped context and calls nothing external; the runtime saves.
/// </summary>
public interface IProjector
{
    /// <summary>Stable and unique; the key in <c>processed_events</c>, <c>consumer_checkpoints</c> and <c>projection_versions</c>.</summary>
    string Name { get; }

    IReadOnlySet<string> EventTypes { get; }

    /// <summary>Aggregate types whose events carry contiguous versions; the runtime refuses to apply past a missing one (arch 8.8.2).</summary>
    IReadOnlySet<string> VersionedAggregateTypes { get; }

    Task ApplyAsync(OutboxMessage message, CancellationToken ct);

    /// <summary>Deletes the projector's read-model rows (replay). Runs inside the runtime's transaction.</summary>
    Task ResetAsync(CancellationToken ct);
}

public enum ReplayOutcome
{
    Done,
    Busy,
    UnknownProjector,
}

/// <summary>Rebuilds one projector from the retained outbox (arch 8.8.2): reset its rows, dedupe marks, versions and checkpoint.</summary>
public interface IProjectionReplayer
{
    Task<ReplayOutcome> ReplayAsync(string name, CancellationToken ct);
}

/// <summary>Lag is now minus the oldest unapplied event's time (0 when none); AsOf is that time, else now.</summary>
public sealed record ProjectorHealth(string Name, long LastSequence, int LagSeconds, DateTimeOffset AsOf, int DeadLetters, bool Lagging);

public sealed record ConsumerDeadLetters(string Consumer, int Count);

public interface IConsumerHealthReader
{
    Task<IReadOnlyList<ProjectorHealth>> GetProjectorsAsync(CancellationToken ct);

    /// <summary>Dead letters of the side-effect consumers (projectors are reported by <see cref="GetProjectorsAsync"/>).</summary>
    Task<IReadOnlyList<ConsumerDeadLetters>> GetConsumerDeadLettersAsync(CancellationToken ct);
}
