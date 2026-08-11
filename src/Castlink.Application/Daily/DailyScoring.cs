namespace Castlink.Application.Daily;

/// <summary>
/// The scoring formula from docs/PLAN.md Phase 4: base 1000, a flat penalty per degree over the
/// challenge's optimal length, floored at zero. Only ever applied to a path that has already passed
/// <see cref="IDailyPathValidator"/> — a fabricated/malformed submission is rejected outright and
/// never scored (see <c>DailySubmissionService</c>).
/// </summary>
public static class DailyScoring
{
    public const int BaseScore = 1000;

    public const int PenaltyPerDegreeOverOptimal = 150;

    public static int Score(int optimalLength, int actualLength)
    {
        var degreesOver = Math.Max(0, actualLength - optimalLength);
        return Math.Max(0, BaseScore - (PenaltyPerDegreeOverOptimal * degreesOver));
    }
}
