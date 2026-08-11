namespace Castlink.Domain;

/// <summary>
/// The deterministically-generated actor pair for a given calendar date (see
/// <c>Castlink.Application.Daily.DailyChallengeGenerator</c>). <see cref="OptimalLength"/> and
/// <see cref="CanonicalPath"/> are persisted at generation time and never recomputed afterwards —
/// a later re-ingest changing the graph must not silently change the answer to a challenge players
/// have already submitted against (see docs/PLAN.md Phase 4).
/// </summary>
public sealed class DailyChallenge
{
    /// <summary>Primary key — one challenge per calendar date, UTC.</summary>
    public DateOnly Date { get; set; }

    public int FromPersonId { get; set; }

    public int ToPersonId { get; set; }

    public int OptimalLength { get; set; }

    public required List<PathLinkRecord> CanonicalPath { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }
}
