namespace Castlink.Application.Graph;

/// <summary>
/// One row of the person/film bipartite graph — one cast credit, with the film's <c>vote_count</c>
/// already joined in so <see cref="InMemoryGraphSnapshot"/> never needs a second query to apply the
/// path tie-break rule.
/// </summary>
public sealed record GraphEdgeRecord(int FilmId, int PersonId, int FilmVoteCount);
