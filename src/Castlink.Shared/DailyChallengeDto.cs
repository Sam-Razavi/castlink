namespace Castlink.Shared;

/// <summary>
/// The response for <c>GET /api/daily</c>. Deliberately carries only display info for each end of
/// the pair — no <c>optimalLength</c>, no path. See docs/PLAN.md Phase 4's hard rule: "GET
/// /api/daily must not leak the answer."
/// </summary>
public sealed record DailyChallengeDto(DateOnly Date, PersonSummaryDto FromPerson, PersonSummaryDto ToPerson);
