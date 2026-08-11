using Castlink.Application.Daily;
using StackExchange.Redis;

namespace Castlink.Infrastructure.Daily;

/// <summary>
/// <c>SET key value NX EX 10</c> — the standard Redis distributed-lock idiom. A short TTL means a
/// crashed request never permanently wedges a player out; it just has to wait out the window.
/// </summary>
internal sealed class RedisDailySubmissionRateLimiter : IDailySubmissionRateLimiter
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(10);

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisDailySubmissionRateLimiter(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public Task<bool> TryAcquireAsync(DateOnly date, Guid playerId, CancellationToken cancellationToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        return database.StringSetAsync(LockKey(date, playerId), "1", LockDuration, When.NotExists);
    }

    private static string LockKey(DateOnly date, Guid playerId) => $"daily-submit-lock:{date:yyyy-MM-dd}:{playerId}";
}
