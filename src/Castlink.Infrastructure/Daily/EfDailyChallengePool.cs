using Castlink.Application.Daily;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Castlink.Infrastructure.Daily;

/// <summary>
/// <see cref="IDailyChallengePool"/> backed by <c>people.credit_count</c>/<c>popularity</c> —
/// exactly the two denormalised columns PLAN.md's eligibility rule needs, so this is a single
/// indexed query rather than anything joining through <c>credits</c>.
/// </summary>
internal sealed class EfDailyChallengePool : IDailyChallengePool
{
    private readonly CastlinkDbContext _dbContext;
    private readonly DailyChallengeOptions _options;

    public EfDailyChallengePool(CastlinkDbContext dbContext, IOptions<DailyChallengeOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<int>> GetEligiblePersonIdsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.People
            .AsNoTracking()
            .Where(person => person.CreditCount >= _options.MinCreditCount && person.Popularity >= _options.MinPopularity)
            .OrderBy(person => person.Id)
            .Select(person => person.Id)
            .ToListAsync(cancellationToken);
    }
}
