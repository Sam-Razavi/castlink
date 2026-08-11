using Castlink.Application.Daily;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeLeaderboardStore : ILeaderboardStore
{
    public List<(DateOnly Date, Guid PlayerId, int Score, int DurationMs)> Submissions { get; } = [];

    public Task SubmitAsync(DateOnly date, Guid playerId, int score, int durationMs, CancellationToken cancellationToken)
    {
        Submissions.Add((date, playerId, score, durationMs));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LeaderboardEntryRecord>> GetTopAsync(DateOnly date, int top, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LeaderboardEntryRecord>>([]);
}
