namespace Castlink.Application.Daily;

/// <summary>
/// Batched lookup of real (person, film) credits, feeding <see cref="IDailyPathValidator"/>. One
/// round trip for every film id mentioned in a submission — never a per-step query.
/// </summary>
public interface ICreditLookupRepository
{
    Task<IReadOnlySet<(int PersonId, int FilmId)>> GetCreditsForFilmsAsync(
        IReadOnlyCollection<int> filmIds, CancellationToken cancellationToken);
}
