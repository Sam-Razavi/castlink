namespace Castlink.Application.People;

public sealed record SharedFilmSummary(int Id, string Title, string? PosterPath);

/// <summary>
/// Films two people are both credited on — what lets the daily-challenge chain-builder UI resolve
/// a real connecting film for a hop the player picks, instead of the server silently inferring one
/// (see docs/PLAN.md Phase 4).
/// </summary>
public interface ISharedFilmsRepository
{
    Task<IReadOnlyList<SharedFilmSummary>> GetSharedFilmsAsync(int fromPersonId, int toPersonId, CancellationToken cancellationToken);
}
