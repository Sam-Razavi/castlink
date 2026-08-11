using Castlink.Domain;

namespace Castlink.Application.Daily;

public enum DailySubmissionOutcome
{
    Accepted,
    RateLimited,
    AlreadySubmitted,
    ChallengeNotFound,
    InvalidPath,
}

/// <summary>Mirrors <c>PathResult</c>'s Found/NotFound convention — see
/// <see cref="PathValidationResult"/> for the same pattern one layer down.
/// <see cref="CanonicalPath"/> is only ever populated on <see cref="DailySubmissionOutcome.Accepted"/>
/// — this is the single place the answer to a challenge is revealed (see docs/PLAN.md Phase 4:
/// "GET /api/daily must not leak the answer").</summary>
public sealed record DailySubmissionResult(
    DailySubmissionOutcome Outcome,
    int PathLength,
    int OptimalLength,
    int Score,
    IReadOnlyList<PathLinkRecord> CanonicalPath)
{
    public static DailySubmissionResult Accepted(int pathLength, int optimalLength, int score, IReadOnlyList<PathLinkRecord> canonicalPath) =>
        new(DailySubmissionOutcome.Accepted, pathLength, optimalLength, score, canonicalPath);

    public static DailySubmissionResult Rejected(DailySubmissionOutcome outcome)
    {
        if (outcome == DailySubmissionOutcome.Accepted)
        {
            throw new ArgumentException("Use Accepted(...) for a successful result.", nameof(outcome));
        }

        return new DailySubmissionResult(outcome, 0, 0, 0, []);
    }
}
