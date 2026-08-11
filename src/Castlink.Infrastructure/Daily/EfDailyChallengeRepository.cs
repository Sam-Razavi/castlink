using System.Text.Json;
using Castlink.Application.Daily;
using Castlink.Domain;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;

namespace Castlink.Infrastructure.Daily;

/// <summary>
/// EF-backed <see cref="IDailyChallengeRepository"/> that read-throughs <see cref="IDistributedCache"/>
/// (Redis) for lookups — the "distributed cache for daily-challenge metadata" bullet from
/// docs/PLAN.md's "Resolved: what Redis is actually for" section. Safe with a long TTL because a
/// challenge row is immutable once generated. This caching is entirely internal to the adapter —
/// <see cref="DailyChallengeService"/> and everything above it stay cache-agnostic, the same way
/// <c>EfPersonSearchRepository</c> owns its own query shape without Application knowing.
/// </summary>
internal sealed class EfDailyChallengeRepository : IDailyChallengeRepository
{
    private static readonly JsonSerializerOptions CacheSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly CastlinkDbContext _dbContext;
    private readonly IDistributedCache _cache;

    public EfDailyChallengeRepository(CastlinkDbContext dbContext, IDistributedCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<DailyChallenge?> FindAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetStringAsync(CacheKey(date), cancellationToken);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<DailyChallenge>(cached, CacheSerializerOptions);
        }

        var challenge = await _dbContext.DailyChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Date == date, cancellationToken);

        if (challenge is not null)
        {
            await CacheAsync(challenge, cancellationToken);
        }

        return challenge;
    }

    public async Task<DailyChallenge> InsertIfNotExistsAsync(DailyChallenge challenge, CancellationToken cancellationToken)
    {
        _dbContext.DailyChallenges.Add(challenge);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost the race to a concurrent generator for the same date — both computed the same
            // deterministic answer, so whichever row landed first is equally correct to serve.
            _dbContext.Entry(challenge).State = EntityState.Detached;
            var existing = await _dbContext.DailyChallenges
                .AsNoTracking()
                .FirstAsync(c => c.Date == challenge.Date, cancellationToken);
            await CacheAsync(existing, cancellationToken);
            return existing;
        }

        await CacheAsync(challenge, cancellationToken);
        return challenge;
    }

    private async Task CacheAsync(DailyChallenge challenge, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(challenge, CacheSerializerOptions);
        await _cache.SetStringAsync(
            CacheKey(challenge.Date),
            json,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
            cancellationToken);
    }

    private static string CacheKey(DateOnly date) => $"daily:{date:yyyy-MM-dd}:row";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
