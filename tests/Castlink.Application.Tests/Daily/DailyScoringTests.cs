using Castlink.Application.Daily;
using Xunit;

namespace Castlink.Application.Tests.Daily;

public sealed class DailyScoringTests
{
    [Theory]
    [InlineData(2, 2, 1000)] // exactly optimal
    [InlineData(2, 3, 850)] // one degree over
    [InlineData(2, 4, 700)] // two degrees over
    [InlineData(2, 8, 100)] // far over, still above the floor
    [InlineData(2, 9, 0)] // far enough over to floor at zero
    [InlineData(2, 20, 0)] // wildly over — still floors at zero, never negative
    [InlineData(4, 2, 1000)] // shorter than optimal can't happen from a real BFS answer, but the
                              // formula must not over-reward it either — no bonus, just full score
    public void Score_matches_the_base_minus_penalty_formula(int optimalLength, int actualLength, int expectedScore)
    {
        var score = DailyScoring.Score(optimalLength, actualLength);

        Assert.Equal(expectedScore, score);
    }
}
