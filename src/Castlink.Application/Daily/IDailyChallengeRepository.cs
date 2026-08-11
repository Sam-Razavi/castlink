using Castlink.Domain;

namespace Castlink.Application.Daily;

public interface IDailyChallengeRepository
{
    Task<DailyChallenge?> FindAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="challenge"/> unless a row for its date already exists, in which case
    /// the existing row is returned instead of throwing. Safe under concurrent callers because two
    /// generators racing for the same date compute the same deterministic answer — whichever row
    /// lands first is equally correct to serve.
    /// </summary>
    Task<DailyChallenge> InsertIfNotExistsAsync(DailyChallenge challenge, CancellationToken cancellationToken);
}
