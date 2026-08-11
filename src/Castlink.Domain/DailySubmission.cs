namespace Castlink.Domain;

/// <summary>
/// A player's scored attempt at a given date's <see cref="DailyChallenge"/>. Unique on
/// (<see cref="Date"/>, <see cref="PlayerId"/>) — one attempt per player per day (see docs/PLAN.md
/// Phase 4).
/// </summary>
public sealed class DailySubmission
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public Guid PlayerId { get; set; }

    public required List<PathLinkRecord> Path { get; set; }

    public int PathLength { get; set; }

    public int Score { get; set; }

    public int DurationMs { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
}
