using Castlink.Application.Daily;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.Daily;

internal sealed class EfCreditLookupRepository : ICreditLookupRepository
{
    private readonly CastlinkDbContext _dbContext;

    public EfCreditLookupRepository(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlySet<(int PersonId, int FilmId)>> GetCreditsForFilmsAsync(
        IReadOnlyCollection<int> filmIds, CancellationToken cancellationToken)
    {
        if (filmIds.Count == 0)
        {
            return new HashSet<(int, int)>();
        }

        var rows = await _dbContext.Credits
            .AsNoTracking()
            .Where(credit => filmIds.Contains(credit.FilmId))
            .Select(credit => new { credit.PersonId, credit.FilmId })
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.PersonId, row.FilmId)).ToHashSet();
    }
}
