using Castlink.Application.Graph;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.Graph;

internal sealed class EfPathEnrichmentRepository : IPathEnrichmentRepository
{
    private readonly CastlinkDbContext _dbContext;

    public EfPathEnrichmentRepository(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<int, PersonSummary>> GetPeopleAsync(
        IReadOnlyCollection<int> personIds, CancellationToken cancellationToken)
    {
        if (personIds.Count == 0)
        {
            return new Dictionary<int, PersonSummary>();
        }

        return await _dbContext.People
            .AsNoTracking()
            .Where(person => personIds.Contains(person.Id))
            .Select(person => new PersonSummary(person.Id, person.Name))
            .ToDictionaryAsync(summary => summary.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, FilmSummary>> GetFilmsAsync(
        IReadOnlyCollection<int> filmIds, CancellationToken cancellationToken)
    {
        if (filmIds.Count == 0)
        {
            return new Dictionary<int, FilmSummary>();
        }

        return await _dbContext.Films
            .AsNoTracking()
            .Where(film => filmIds.Contains(film.Id))
            .Select(film => new FilmSummary(film.Id, film.Title))
            .ToDictionaryAsync(summary => summary.Id, cancellationToken);
    }
}
