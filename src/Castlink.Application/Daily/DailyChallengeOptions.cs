namespace Castlink.Application.Daily;

/// <summary>
/// Binds the "DailyChallenge" configuration section — tunables for deterministic daily-pair
/// generation (see docs/PLAN.md Phase 4). Nothing here is a secret; all defaults are safe to check
/// in.
/// </summary>
public sealed class DailyChallengeOptions
{
    public const string SectionName = "DailyChallenge";

    /// <summary>Namespacing constant folded into the generation seed. Not secret — changing it
    /// simply reshuffles every future day's pair, it isn't a security boundary.</summary>
    public string Salt { get; set; } = "castlink-daily-v1";

    /// <summary>
    /// Popularity floor for the eligible pool, alongside <see cref="MinCreditCount"/>. Placeholder
    /// default — needs tuning against the real ingested `people.popularity` distribution, which
    /// isn't queryable without a live, seeded Postgres (not available in this sandbox).
    /// </summary>
    public double MinPopularity { get; set; } = 5.0;

    /// <summary>PLAN.md's literal pool-eligibility rule: both actors must be well-credited enough
    /// to be recognisable.</summary>
    public int MinCreditCount { get; set; } = 8;

    public int MinOptimalLength { get; set; } = 2;

    public int MaxOptimalLength { get; set; } = 4;

    /// <summary>Safety bound so a pathologically small or disconnected pool can't loop forever.</summary>
    public int MaxGenerationAttempts { get; set; } = 2000;
}
