using Castlink.Application.Daily;
using Castlink.Application.Graph;
using Castlink.Application.Tests.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace Castlink.Application.Tests.Daily;

public sealed class DailyChallengeGeneratorTests
{
    private static readonly DateOnly Date = new(2026, 8, 11);

    // Star topology: person 1 shares a film with each of 2..6, so any pair drawn from {2..6} has a
    // real 2-degree path through person 1, while any pair involving person 1 itself is only
    // 1 degree (too short — excluded by MinOptimalLength). Plenty of in-range pairs, so the real
    // generator (real BidirectionalPathFinder, real SHA256-seeded Random) succeeds within the
    // default attempt budget without needing to know which specific attempt lands.
    private static InMemoryGraphSnapshot BuildStarGraph() => InMemoryGraphSnapshot.Build(
    [
        new GraphEdgeRecord(101, 1, 100), new GraphEdgeRecord(101, 2, 100),
        new GraphEdgeRecord(102, 1, 100), new GraphEdgeRecord(102, 3, 100),
        new GraphEdgeRecord(103, 1, 100), new GraphEdgeRecord(103, 4, 100),
        new GraphEdgeRecord(104, 1, 100), new GraphEdgeRecord(104, 5, 100),
        new GraphEdgeRecord(105, 1, 100), new GraphEdgeRecord(105, 6, 100),
    ]);

    private static DailyChallengeGenerator BuildGenerator(IDailyChallengePool pool, IPathFinder pathFinder, DailyChallengeOptions? options = null) =>
        new(pool, pathFinder, Options.Create(options ?? new DailyChallengeOptions()));

    [Fact]
    public async Task Same_date_and_salt_produce_the_same_challenge_across_multiple_calls()
    {
        var pool = new FakeDailyChallengePool([1, 2, 3, 4, 5, 6]);
        var graph = BuildStarGraph();
        var generator = BuildGenerator(pool, new BidirectionalPathFinder());

        var first = await generator.GenerateAsync(Date, graph, CancellationToken.None);
        var second = await generator.GenerateAsync(Date, graph, CancellationToken.None);

        Assert.Equal(first.FromPersonId, second.FromPersonId);
        Assert.Equal(first.ToPersonId, second.ToPersonId);
        Assert.Equal(first.OptimalLength, second.OptimalLength);
        Assert.Equal(first.CanonicalPath, second.CanonicalPath);

        // And the generated pair is a real acceptable answer, not just "the same by coincidence".
        Assert.InRange(first.OptimalLength, 2, 4);
        Assert.NotEqual(first.FromPersonId, first.ToPersonId);
    }

    [Fact]
    public async Task A_different_date_can_produce_a_different_challenge()
    {
        var pool = new FakeDailyChallengePool([1, 2, 3, 4, 5, 6]);
        var graph = BuildStarGraph();
        var generator = BuildGenerator(pool, new BidirectionalPathFinder());

        var day1 = await generator.GenerateAsync(Date, graph, CancellationToken.None);
        var day2 = await generator.GenerateAsync(Date.AddDays(1), graph, CancellationToken.None);

        // Not a strict inequality assertion (a collision is possible, if unlikely) — the point is
        // both are independently valid, deterministically-reproducible answers.
        Assert.InRange(day1.OptimalLength, 2, 4);
        Assert.InRange(day2.OptimalLength, 2, 4);
    }

    [Fact]
    public async Task Advances_the_seed_and_retries_until_an_acceptable_pair_is_found()
    {
        var pool = new FakeDailyChallengePool([1, 2]);
        var tooShort = PathResult.Found([new PathLink(1, 100, 2)]); // 1 degree — below MinOptimalLength
        var noConnection = PathResult.NotFound(PathOutcome.NoConnectionWithinDepth);
        var accepted = PathResult.Found([new PathLink(1, 100, 2), new PathLink(2, 200, 3), new PathLink(3, 300, 4)]); // 3 degrees
        var fakeFinder = new FakePathFinder([tooShort, noConnection, accepted]);
        var generator = BuildGenerator(pool, fakeFinder);

        var result = await generator.GenerateAsync(Date, InMemoryGraphSnapshot.Empty, CancellationToken.None);

        Assert.Equal(3, fakeFinder.CallCount);
        Assert.Equal(3, result.OptimalLength);
        Assert.Equal(accepted.Links, result.CanonicalPath);
    }

    [Fact]
    public async Task Throws_after_exhausting_the_attempt_budget_with_no_acceptable_pair()
    {
        var pool = new FakeDailyChallengePool([1, 2]);
        var fakeFinder = new FakePathFinder([], fallback: PathResult.NotFound(PathOutcome.NoConnectionWithinDepth));
        var generator = BuildGenerator(pool, fakeFinder, new DailyChallengeOptions { MaxGenerationAttempts = 5 });

        await Assert.ThrowsAsync<DailyChallengeGenerationException>(
            () => generator.GenerateAsync(Date, InMemoryGraphSnapshot.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_immediately_when_the_pool_has_fewer_than_two_people()
    {
        var pool = new FakeDailyChallengePool([1]);
        var fakeFinder = new FakePathFinder([]);
        var generator = BuildGenerator(pool, fakeFinder);

        await Assert.ThrowsAsync<DailyChallengeGenerationException>(
            () => generator.GenerateAsync(Date, InMemoryGraphSnapshot.Empty, CancellationToken.None));
        Assert.Equal(0, fakeFinder.CallCount);
    }
}
