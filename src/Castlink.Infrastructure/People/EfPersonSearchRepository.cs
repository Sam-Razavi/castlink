using Castlink.Application.People;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.People;

/// <summary>
/// One query, two conditions: a plain substring/prefix match (`ILIKE '%query%'` — the common
/// typeahead case) OR'd with a trigram similarity check (typo tolerance), ranked by similarity
/// then popularity. Both conditions use the `gin_trgm_ops` GIN index already created in Phase 1
/// (see <c>PersonConfiguration</c>) — nothing new to migrate.
/// </summary>
internal sealed class EfPersonSearchRepository : IPersonSearchRepository
{
    private readonly CastlinkDbContext _dbContext;

    public EfPersonSearchRepository(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PersonSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var substringPattern = $"%{query}%";

        return await _dbContext.People
            .AsNoTracking()
            .Where(person =>
                EF.Functions.ILike(person.Name, substringPattern)
                || EF.Functions.TrigramsAreSimilar(person.Name, query))
            .OrderByDescending(person => EF.Functions.TrigramsSimilarity(person.Name, query))
            .ThenByDescending(person => person.Popularity)
            .Take(limit)
            .Select(person => new PersonSearchResult(person.Id, person.Name, person.ProfilePath, person.Popularity))
            .ToListAsync(cancellationToken);
    }
}
