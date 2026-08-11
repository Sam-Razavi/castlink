using Castlink.Application.People;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.People;

internal sealed class EfSharedFilmsRepository : ISharedFilmsRepository
{
    private readonly CastlinkDbContext _dbContext;

    public EfSharedFilmsRepository(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SharedFilmSummary>> GetSharedFilmsAsync(int fromPersonId, int toPersonId, CancellationToken cancellationToken)
    {
        var fromFilmIds = _dbContext.Credits
            .Where(credit => credit.PersonId == fromPersonId)
            .Select(credit => credit.FilmId);

        return await _dbContext.Credits
            .Where(credit => credit.PersonId == toPersonId && fromFilmIds.Contains(credit.FilmId))
            .Join(_dbContext.Films, credit => credit.FilmId, film => film.Id, (_, film) => film)
            .Select(film => new SharedFilmSummary(film.Id, film.Title, film.PosterPath))
            .ToListAsync(cancellationToken);
    }
}
