using Castlink.Domain;

namespace Castlink.Application.Daily;

public interface IPlayerRepository
{
    Task InsertAsync(Player player, CancellationToken cancellationToken);

    /// <summary>Batched lookup for leaderboard display-name resolution — never one query per row.</summary>
    Task<IReadOnlyDictionary<Guid, Player>> GetByIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken);
}
