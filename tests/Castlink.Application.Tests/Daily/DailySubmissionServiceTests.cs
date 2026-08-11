using Castlink.Application.Daily;
using Castlink.Application.Tests.Fakes;
using Castlink.Domain;
using Xunit;

namespace Castlink.Application.Tests.Daily;

public sealed class DailySubmissionServiceTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);
    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Same fixture shape as DailyPathValidatorTests: 1 --film10--> 2 --film20--> 3.
    private static readonly IReadOnlySet<(int, int)> KnownCredits = new HashSet<(int, int)>
    {
        (1, 10), (2, 10), (2, 20), (3, 20),
    };

    private static readonly DailyChallenge Challenge = new()
    {
        Date = Today,
        FromPersonId = 1,
        ToPersonId = 3,
        OptimalLength = 2,
        CanonicalPath = [new PathLinkRecord(1, 10, 2), new PathLinkRecord(2, 20, 3)],
        GeneratedAt = DateTimeOffset.UtcNow,
    };

    private static readonly IReadOnlyList<DailyPathStepRecord> ValidSubmittedPath =
    [
        new DailyPathStepRecord(1, 10), new DailyPathStepRecord(2, 20), new DailyPathStepRecord(3, null),
    ];

    private sealed record Fixture(
        DailySubmissionService Service,
        FakeDailySubmissionRateLimiter RateLimiter,
        FakeCreditLookupRepository CreditLookup,
        FakeDailySubmissionRepository SubmissionRepository,
        FakeLeaderboardStore LeaderboardStore);

    private static Fixture BuildFixture(DailyChallenge? challenge, bool rateLimiterAllows = true)
    {
        var rateLimiter = new FakeDailySubmissionRateLimiter(rateLimiterAllows);
        var creditLookup = new FakeCreditLookupRepository(KnownCredits);
        var submissionRepository = new FakeDailySubmissionRepository();
        var leaderboardStore = new FakeLeaderboardStore();

        var service = new DailySubmissionService(
            rateLimiter,
            new FakeDailyChallengeRepository(challenge),
            creditLookup,
            new DailyPathValidator(),
            submissionRepository,
            leaderboardStore,
            new FixedTimeProvider(Today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));

        return new Fixture(service, rateLimiter, creditLookup, submissionRepository, leaderboardStore);
    }

    [Fact]
    public async Task An_accepted_submission_is_persisted_once_and_published_to_the_leaderboard_once()
    {
        var fixture = BuildFixture(Challenge);

        var result = await fixture.Service.SubmitAsync(PlayerId, ValidSubmittedPath, durationMs: 5000, CancellationToken.None);

        Assert.Equal(DailySubmissionOutcome.Accepted, result.Outcome);
        Assert.Equal(2, result.PathLength);
        Assert.Equal(2, result.OptimalLength);
        Assert.Equal(1000, result.Score); // exactly optimal — full score
        Assert.Equal(Challenge.CanonicalPath, result.CanonicalPath);

        Assert.Single(fixture.SubmissionRepository.Submissions);
        Assert.Single(fixture.LeaderboardStore.Submissions);
        Assert.Equal((Today, PlayerId, 1000, 5000), fixture.LeaderboardStore.Submissions[0]);
    }

    [Fact]
    public async Task A_duplicate_submission_is_rejected_and_does_not_publish_a_second_leaderboard_entry()
    {
        var fixture = BuildFixture(Challenge);
        await fixture.Service.SubmitAsync(PlayerId, ValidSubmittedPath, durationMs: 5000, CancellationToken.None);

        var second = await fixture.Service.SubmitAsync(PlayerId, ValidSubmittedPath, durationMs: 4000, CancellationToken.None);

        Assert.Equal(DailySubmissionOutcome.AlreadySubmitted, second.Outcome);
        Assert.Single(fixture.SubmissionRepository.Submissions);
        Assert.Single(fixture.LeaderboardStore.Submissions);
    }

    [Fact]
    public async Task A_rate_limited_submission_short_circuits_before_touching_credits_or_persistence()
    {
        var fixture = BuildFixture(Challenge, rateLimiterAllows: false);

        var result = await fixture.Service.SubmitAsync(PlayerId, ValidSubmittedPath, durationMs: 5000, CancellationToken.None);

        Assert.Equal(DailySubmissionOutcome.RateLimited, result.Outcome);
        Assert.Equal(0, fixture.CreditLookup.CallCount);
        Assert.Empty(fixture.SubmissionRepository.Submissions);
        Assert.Empty(fixture.LeaderboardStore.Submissions);
    }

    [Fact]
    public async Task An_invalid_path_is_rejected_and_never_persisted()
    {
        var fixture = BuildFixture(Challenge);
        var fabricatedPath = new[] { new DailyPathStepRecord(1, 999), new DailyPathStepRecord(3, null) };

        var result = await fixture.Service.SubmitAsync(PlayerId, fabricatedPath, durationMs: 5000, CancellationToken.None);

        Assert.Equal(DailySubmissionOutcome.InvalidPath, result.Outcome);
        Assert.Empty(fixture.SubmissionRepository.Submissions);
        Assert.Empty(fixture.LeaderboardStore.Submissions);
    }

    [Fact]
    public async Task A_missing_challenge_for_the_date_is_reported_distinctly()
    {
        var fixture = BuildFixture(challenge: null);

        var result = await fixture.Service.SubmitAsync(PlayerId, ValidSubmittedPath, durationMs: 5000, CancellationToken.None);

        Assert.Equal(DailySubmissionOutcome.ChallengeNotFound, result.Outcome);
        Assert.Empty(fixture.SubmissionRepository.Submissions);
    }
}
