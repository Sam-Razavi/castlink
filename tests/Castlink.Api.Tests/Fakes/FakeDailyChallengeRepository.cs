using Castlink.Application.Daily;
using Castlink.Domain;

namespace Castlink.Api.Tests.Fakes;

/// <summary>Always returns the same fixture challenge, regardless of the queried date — the tests
/// that use this also pin <c>TimeProvider</c> via <see cref="FixedTimeProvider"/>, so "today" is
/// always the fixture's date anyway; ignoring the parameter just keeps the fake trivial.</summary>
internal sealed class FakeDailyChallengeRepository : IDailyChallengeRepository
{
    private readonly DailyChallenge _challenge;

    public FakeDailyChallengeRepository(DailyChallenge challenge)
    {
        _challenge = challenge;
    }

    public Task<DailyChallenge?> FindAsync(DateOnly date, CancellationToken cancellationToken) =>
        Task.FromResult<DailyChallenge?>(_challenge);

    public Task<DailyChallenge> InsertIfNotExistsAsync(DailyChallenge challenge, CancellationToken cancellationToken) =>
        Task.FromResult(challenge);
}
