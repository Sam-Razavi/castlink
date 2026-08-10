namespace Castlink.Application.Graph;

/// <summary>
/// Read-only, dense-index CSR (compressed sparse row) view of the actor/film bipartite graph.
/// Indices are internal to one snapshot instance — callers translate a TMDB id to an index once
/// via <see cref="TryGetPersonIndex"/>/<see cref="TryGetFilmIndex"/>, then work purely in index
/// space (flat array access, no dictionary lookups) for the rest of a query. See docs/PLAN.md
/// Phase 2 for the sizing rationale.
/// </summary>
public interface IGraphSnapshot
{
    int PersonCount { get; }

    int FilmCount { get; }

    bool TryGetPersonIndex(int personId, out int personIndex);

    bool TryGetFilmIndex(int filmId, out int filmIndex);

    int PersonIdAt(int personIndex);

    int FilmIdAt(int filmIndex);

    /// <summary>TMDB <c>vote_count</c> for the film at this index — what the path tie-break sorts on.</summary>
    int VoteCountAt(int filmIndex);

    /// <summary>Every film a person has a credit on, as film indices.</summary>
    ReadOnlySpan<int> FilmIndexesForPerson(int personIndex);

    /// <summary>Every person credited on a film, as person indices.</summary>
    ReadOnlySpan<int> PersonIndexesForFilm(int filmIndex);
}
