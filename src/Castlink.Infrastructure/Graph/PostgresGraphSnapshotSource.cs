using Castlink.Application.Graph;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.Graph;

/// <summary>
/// One streaming query, joining <c>credits</c> with <c>films</c> so the film's <c>vote_count</c> —
/// needed for the path tie-break — comes along for free, no second round trip.
/// </summary>
internal sealed class PostgresGraphSnapshotSource : IGraphSnapshotSource
{
    private readonly CastlinkDbContext _dbContext;

    public PostgresGraphSnapshotSource(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IAsyncEnumerable<GraphEdgeRecord> StreamEdgesAsync(CancellationToken cancellationToken) =>
        _dbContext.Credits
            .AsNoTracking()
            .Join(
                _dbContext.Films.AsNoTracking(),
                credit => credit.FilmId,
                film => film.Id,
                (credit, film) => new GraphEdgeRecord(credit.FilmId, credit.PersonId, film.VoteCount))
            .AsAsyncEnumerable();
}
