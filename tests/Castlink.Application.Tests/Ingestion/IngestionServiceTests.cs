using Castlink.Application.Ingestion;
using Castlink.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Castlink.Application.Tests.Ingestion;

public sealed class IngestionServiceTests
{
    private static MovieIngestionRecord Movie(int id, params int[] castPersonIds) =>
        new(
            FilmId: id,
            Title: $"Film {id}",
            ReleaseYear: 2000,
            Popularity: 10,
            VoteCount: 500,
            PosterPath: null,
            Cast: [.. castPersonIds.Select(pid => new CastCreditRecord(pid, $"Person {pid}", 5, null, "Acting", 0, null))]);

    private static IngestionService CreateService(
        FakeTmdbClient tmdbClient,
        FakeIngestionWriter writer,
        FakeSyncStateStore syncState,
        FakeIngestionRunTracker runTracker,
        DateTimeOffset now) =>
        new(tmdbClient, writer, syncState, runTracker, NullLogger<IngestionService>.Instance, new FixedTimeProvider(now));

    [Fact]
    public async Task RunFullSeedAsync_discovers_fetches_and_writes_every_year_in_range()
    {
        var tmdbClient = new FakeTmdbClient
        {
            DiscoverMovieIds = (year, _) => year switch
            {
                2000 => [1, 2],
                2001 => [3],
                _ => [],
            },
            GetMovie = id => Movie(id, 100 + id),
        };
        var writer = new FakeIngestionWriter();
        var syncState = new FakeSyncStateStore();
        var runTracker = new FakeIngestionRunTracker();
        var service = CreateService(tmdbClient, writer, syncState, runTracker, new DateTimeOffset(2001, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var summary = await service.RunFullSeedAsync(startYear: 2000, CancellationToken.None);

        Assert.Equal([2000, 2001], tmdbClient.DiscoveredYears);
        Assert.Equal(3, summary.FilmsWritten);
        Assert.Equal(1, runTracker.StartCalls);
        Assert.Equal((3, 3), runTracker.Completed);
    }

    [Fact]
    public async Task RunFullSeedAsync_advances_watermark_and_a_rerun_does_not_refetch_completed_years()
    {
        var tmdbClient = new FakeTmdbClient
        {
            DiscoverMovieIds = (_, _) => [1],
            GetMovie = id => Movie(id),
        };
        var writer = new FakeIngestionWriter();
        var syncState = new FakeSyncStateStore();
        var now = new DateTimeOffset(2000, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var service = CreateService(tmdbClient, writer, syncState, new FakeIngestionRunTracker(), now);

        await service.RunFullSeedAsync(startYear: 2000, CancellationToken.None);
        Assert.Equal("2000", await syncState.GetAsync(IngestionService.FullSeedWatermarkKey, CancellationToken.None));
        Assert.Single(writer.Batches);

        // Re-run with the same "now" (current year already fully seeded per the watermark) —
        // must not discover or write anything a second time.
        await service.RunFullSeedAsync(startYear: 2000, CancellationToken.None);

        Assert.Single(tmdbClient.DiscoveredYears);
        Assert.Single(writer.Batches);
    }

    [Fact]
    public async Task RunIncrementalSyncAsync_chunks_a_stale_watermark_into_14_day_windows()
    {
        var tmdbClient = new FakeTmdbClient { GetChangedMovieIds = (_, _) => [] };
        var writer = new FakeIngestionWriter();
        var syncState = new FakeSyncStateStore();
        var today = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
        // 30 days stale — must not fetch a single 30-day window (exceeds TMDB's 14-day cap).
        await syncState.SetAsync(IngestionService.IncrementalSyncWatermarkKey, "2026-01-01", CancellationToken.None);
        var service = CreateService(tmdbClient, writer, syncState, new FakeIngestionRunTracker(), today);

        await service.RunIncrementalSyncAsync(CancellationToken.None);

        Assert.All(tmdbClient.ChangesWindows, w => Assert.True((w.End.ToDateTime(TimeOnly.MinValue) - w.Start.ToDateTime(TimeOnly.MinValue)).Days <= 14));
        Assert.Equal(new DateOnly(2026, 1, 1), tmdbClient.ChangesWindows[0].Start);
        Assert.Equal(new DateOnly(2026, 1, 31), tmdbClient.ChangesWindows[^1].End);
        Assert.Equal("2026-01-31", await syncState.GetAsync(IngestionService.IncrementalSyncWatermarkKey, CancellationToken.None));
    }

    [Fact]
    public async Task RunIncrementalSyncAsync_defaults_to_yesterday_when_no_watermark_exists()
    {
        var tmdbClient = new FakeTmdbClient { GetChangedMovieIds = (_, _) => [] };
        var today = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var service = CreateService(tmdbClient, new FakeIngestionWriter(), new FakeSyncStateStore(), new FakeIngestionRunTracker(), today);

        await service.RunIncrementalSyncAsync(CancellationToken.None);

        var window = Assert.Single(tmdbClient.ChangesWindows);
        Assert.Equal(new DateOnly(2026, 3, 9), window.Start);
        Assert.Equal(new DateOnly(2026, 3, 10), window.End);
    }

    [Fact]
    public async Task FetchAndWriteAsync_skips_movies_tmdb_no_longer_has_without_throwing()
    {
        var tmdbClient = new FakeTmdbClient
        {
            DiscoverMovieIds = (_, _) => [1, 2, 3],
            GetMovie = id => id == 2 ? null : Movie(id), // id 2 was deleted upstream
        };
        var writer = new FakeIngestionWriter();
        var service = CreateService(tmdbClient, writer, new FakeSyncStateStore(), new FakeIngestionRunTracker(),
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var summary = await service.RunFullSeedAsync(startYear: 2000, CancellationToken.None);

        Assert.Equal(2, summary.FilmsWritten);
        Assert.Single(writer.Batches);
        Assert.DoesNotContain(writer.Batches[0], m => m.FilmId == 2);
    }

    [Fact]
    public async Task RunFullSeedAsync_flushes_in_batches_of_200()
    {
        var ids = Enumerable.Range(1, 250).ToArray();
        var tmdbClient = new FakeTmdbClient
        {
            DiscoverMovieIds = (_, _) => ids,
            GetMovie = id => Movie(id),
        };
        var writer = new FakeIngestionWriter();
        var service = CreateService(tmdbClient, writer, new FakeSyncStateStore(), new FakeIngestionRunTracker(),
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await service.RunFullSeedAsync(startYear: 2000, CancellationToken.None);

        Assert.Equal(2, writer.Batches.Count);
        Assert.Equal(200, writer.Batches[0].Count);
        Assert.Equal(50, writer.Batches[1].Count);
    }

    [Fact]
    public async Task A_write_failure_marks_the_run_failed_and_rethrows()
    {
        var tmdbClient = new FakeTmdbClient
        {
            DiscoverMovieIds = (_, _) => [1],
            GetMovie = id => Movie(id),
        };
        var writer = new FakeIngestionWriter { OnUpsert = _ => throw new InvalidOperationException("boom") };
        var runTracker = new FakeIngestionRunTracker();
        var service = CreateService(tmdbClient, writer, new FakeSyncStateStore(), runTracker,
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunFullSeedAsync(startYear: 2000, CancellationToken.None));

        Assert.Equal("boom", runTracker.FailedError);
        Assert.Null(runTracker.Completed);
    }
}
