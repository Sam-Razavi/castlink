namespace Castlink.Application.Daily;

/// <summary>
/// Per-player rate limiting on daily-challenge submissions — the third of the three jobs Redis is
/// scoped to in docs/PLAN.md's "Resolved: what Redis is actually for" section (alongside the
/// SignalR backplane and the leaderboard sorted set). This guards the short window before a
/// submission's DB write lands — e.g. a double-click or a retried request — distinct from the DB's
/// unique index on (date, player_id), which is what actually enforces "one graded attempt per day".
/// </summary>
public interface IDailySubmissionRateLimiter
{
    /// <summary>Returns <c>true</c> if this call acquired the lock (the caller may proceed),
    /// <c>false</c> if another submission for the same date/player is already in flight.</summary>
    Task<bool> TryAcquireAsync(DateOnly date, Guid playerId, CancellationToken cancellationToken);
}
