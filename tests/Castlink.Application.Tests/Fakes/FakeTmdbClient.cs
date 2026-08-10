using Castlink.Application.Ingestion;

namespace Castlink.Application.Tests.Fakes;

/// <summary>In-memory <see cref="ITmdbClient"/> double — no network, fully scriptable per test.</summary>
internal sealed class FakeTmdbClient : ITmdbClient
{
    public Func<int, int, IReadOnlyList<int>> DiscoverMovieIds { get; set; } = (_, _) => [];

    public Func<int, MovieIngestionRecord?> GetMovie { get; set; } = _ => null;

    public Func<DateOnly, DateOnly, IReadOnlyList<int>> GetChangedMovieIds { get; set; } = (_, _) => [];

    public List<int> DiscoveredYears { get; } = [];

    public List<(DateOnly Start, DateOnly End)> ChangesWindows { get; } = [];

    public Task<IReadOnlyList<int>> DiscoverMovieIdsAsync(int year, int minVoteCount, CancellationToken cancellationToken)
    {
        DiscoveredYears.Add(year);
        return Task.FromResult(DiscoverMovieIds(year, minVoteCount));
    }

    public Task<MovieIngestionRecord?> GetMovieWithCreditsAsync(int movieId, CancellationToken cancellationToken) =>
        Task.FromResult(GetMovie(movieId));

    public Task<IReadOnlyList<int>> GetChangedMovieIdsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        ChangesWindows.Add((startDate, endDate));
        return Task.FromResult(GetChangedMovieIds(startDate, endDate));
    }
}
