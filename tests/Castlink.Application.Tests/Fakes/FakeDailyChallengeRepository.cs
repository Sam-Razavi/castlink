using Castlink.Application.Daily;
using Castlink.Domain;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeDailyChallengeRepository : IDailyChallengeRepository
{
    private readonly DailyChallenge? _challenge;

    public FakeDailyChallengeRepository(DailyChallenge? challenge)
    {
        _challenge = challenge;
    }

    public Task<DailyChallenge?> FindAsync(DateOnly date, CancellationToken cancellationToken) =>
        Task.FromResult(_challenge is not null && _challenge.Date == date ? _challenge : null);

    public Task<DailyChallenge> InsertIfNotExistsAsync(DailyChallenge challenge, CancellationToken cancellationToken) =>
        Task.FromResult(challenge);
}
