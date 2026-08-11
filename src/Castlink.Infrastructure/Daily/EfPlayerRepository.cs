using Castlink.Application.Daily;
using Castlink.Domain;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.Daily;

internal sealed class EfPlayerRepository : IPlayerRepository
{
    private readonly CastlinkDbContext _dbContext;

    public EfPlayerRepository(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InsertAsync(Player player, CancellationToken cancellationToken)
    {
        _dbContext.Players.Add(player);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Player>> GetByIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken)
    {
        if (playerIds.Count == 0)
        {
            return new Dictionary<Guid, Player>();
        }

        var players = await _dbContext.Players
            .AsNoTracking()
            .Where(player => playerIds.Contains(player.Id))
            .ToListAsync(cancellationToken);

        return players.ToDictionary(player => player.Id);
    }
}
