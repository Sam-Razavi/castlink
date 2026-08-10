namespace Castlink.Application.Ingestion;

/// <summary>
/// Port to TMDB. Rate limiting, retry, and the actual HTTP calls are an
/// <c>Castlink.Infrastructure</c> concern — this interface only describes what ingestion needs.
/// </summary>
public interface ITmdbClient
{
    /// <summary>
    /// Candidate film ids for a single release year with at least <paramref name="minVoteCount"/>
    /// votes, via <c>/discover/movie</c> (partitioned by year to stay well under its 500-page cap —
    /// see docs/PLAN.md Phase 1 for why this replaced the daily-export approach).
    /// </summary>
    Task<IReadOnlyList<int>> DiscoverMovieIdsAsync(int year, int minVoteCount, CancellationToken cancellationToken);

    /// <summary>
    /// Full detail and cast for one film in a single call (<c>append_to_response=credits</c>).
    /// Returns <see langword="null"/> if TMDB has no such movie (e.g. it was deleted upstream).
    /// </summary>
    Task<MovieIngestionRecord?> GetMovieWithCreditsAsync(int movieId, CancellationToken cancellationToken);

    /// <summary>
    /// Film ids changed within the given window, via <c>/movie/changes</c>. Callers are
    /// responsible for keeping the window within TMDB's 14-day maximum.
    /// </summary>
    Task<IReadOnlyList<int>> GetChangedMovieIdsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
}
