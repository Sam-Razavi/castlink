namespace Castlink.Shared;

/// <summary>
/// The response for a successful <c>POST /api/daily/submit</c>. <see cref="CanonicalPath"/> is the
/// answer — this is the one place it's ever revealed (see docs/PLAN.md Phase 4).
/// </summary>
public sealed record DailySubmitResponseDto(
    int PathLength,
    int OptimalLength,
    int Score,
    IReadOnlyList<PathLinkDto> CanonicalPath);
