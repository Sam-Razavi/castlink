namespace Castlink.Application.Daily;

public sealed record LeaderboardEntryRecord(Guid PlayerId, int Rank, int Score);

/// <summary>
/// The live-rankings sorted set for a given day — the second of the three jobs Redis is scoped to
/// in docs/PLAN.md's "Resolved: what Redis is actually for" section. Exact <c>durationMs</c> is not
/// recoverable from this store (see <c>RedisLeaderboardStore</c>'s tie-break encoding); Postgres
/// remains the source of truth for exact timing, this store only ever needs to answer "who's
/// ranked where".
/// </summary>
public interface ILeaderboardStore
{
    Task SubmitAsync(DateOnly date, Guid playerId, int score, int durationMs, CancellationToken cancellationToken);

    Task<IReadOnlyList<LeaderboardEntryRecord>> GetTopAsync(DateOnly date, int top, CancellationToken cancellationToken);
}
