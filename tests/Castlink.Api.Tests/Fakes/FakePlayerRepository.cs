using Castlink.Application.Daily;
using Castlink.Domain;

namespace Castlink.Api.Tests.Fakes;

internal sealed class FakePlayerRepository : IPlayerRepository
{
    private readonly Dictionary<Guid, Player> _players = new();

    public IReadOnlyList<Player> InsertedPlayers => _players.Values.ToList();

    public Task InsertAsync(Player player, CancellationToken cancellationToken)
    {
        _players[player.Id] = player;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, Player>> GetByIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Player>>(
            playerIds.Where(_players.ContainsKey).ToDictionary(id => id, id => _players[id]));
}
