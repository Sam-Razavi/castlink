using Castlink.Application.Daily;

namespace Castlink.Api.Tests.Fakes;

internal sealed class FakeLeaderboardStore : ILeaderboardStore
{
    private readonly IReadOnlyList<LeaderboardEntryRecord> _entriesToReturn;

    public FakeLeaderboardStore(IReadOnlyList<LeaderboardEntryRecord> entriesToReturn)
    {
        _entriesToReturn = entriesToReturn;
    }

    public List<(DateOnly Date, Guid PlayerId, int Score, int DurationMs)> Submissions { get; } = [];

    public Task SubmitAsync(DateOnly date, Guid playerId, int score, int durationMs, CancellationToken cancellationToken)
    {
        Submissions.Add((date, playerId, score, durationMs));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LeaderboardEntryRecord>> GetTopAsync(DateOnly date, int top, CancellationToken cancellationToken) =>
        Task.FromResult(_entriesToReturn);
}
