namespace Castlink.Application.Graph;

public sealed record PersonSummary(int Id, string Name, string? ProfilePath);

public sealed record FilmSummary(int Id, string Title, string? PosterPath);

/// <summary>
/// Looks up display names/titles for the handful of ids in a found path. Deliberately separate
/// from the BFS itself — <see cref="BidirectionalPathFinder"/> stays metadata-free, and this only
/// ever runs for a few ids at a time (the ones actually in a result), never the whole graph.
/// </summary>
public interface IPathEnrichmentRepository
{
    Task<IReadOnlyDictionary<int, PersonSummary>> GetPeopleAsync(
        IReadOnlyCollection<int> personIds, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, FilmSummary>> GetFilmsAsync(
        IReadOnlyCollection<int> filmIds, CancellationToken cancellationToken);
}
