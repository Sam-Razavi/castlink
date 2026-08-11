using Castlink.Application.Daily;
using StackExchange.Redis;

namespace Castlink.Infrastructure.Daily;

/// <summary>
/// Redis sorted set (<c>ZADD</c>/<c>ZRANGE</c>) — the right data structure for live rankings,
/// avoiding a per-submission Postgres query (see docs/PLAN.md's "Resolved: what Redis is actually
/// for"). Ties are broken by duration via a single encoded score:
/// <c>score - clamp(durationMs, 0, 999_999) / 10_000_000</c>.
///
/// Proof this never perturbs score ranking: for any two integer scores <c>s1 &gt; s2</c> in
/// [0, 1000], the score term's minimum gap (1) always exceeds the duration term's maximum swing
/// (999999 / 1e7 ≈ 0.09999...), so a higher score always encodes higher regardless of duration. For
/// equal scores, ordering reduces to <c>-duration</c>, so a faster (smaller) duration encodes
/// higher — correct, faster wins ties.
///
/// Exact <c>durationMs</c> is not recoverable from the encoded value and isn't reconstructed here —
/// Postgres remains the source of truth for exact timing; this store only ever needs to answer
/// "who's ranked where".
/// </summary>
internal sealed class RedisLeaderboardStore : ILeaderboardStore
{
    private const double DurationDivisor = 10_000_000.0;
    private const int MaxEncodedDurationMs = 999_999;

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisLeaderboardStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public Task SubmitAsync(DateOnly date, Guid playerId, int score, int durationMs, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        return database.SortedSetAddAsync(LeaderboardKey(date), playerId.ToString(), Encode(score, durationMs));
    }

    public async Task<IReadOnlyList<LeaderboardEntryRecord>> GetTopAsync(DateOnly date, int top, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var entries = await database.SortedSetRangeByRankWithScoresAsync(LeaderboardKey(date), 0, top - 1, Order.Descending);

        var results = new List<LeaderboardEntryRecord>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            var playerId = Guid.Parse(entries[i].Element.ToString());
            var score = (int)Math.Round(entries[i].Score);
            results.Add(new LeaderboardEntryRecord(playerId, Rank: i + 1, score));
        }

        return results;
    }

    private static double Encode(int score, int durationMs)
    {
        var clampedDurationMs = Math.Clamp(durationMs, 0, MaxEncodedDurationMs);
        return score - (clampedDurationMs / DurationDivisor);
    }

    private static string LeaderboardKey(DateOnly date) => $"leaderboard:{date:yyyy-MM-dd}";
}
