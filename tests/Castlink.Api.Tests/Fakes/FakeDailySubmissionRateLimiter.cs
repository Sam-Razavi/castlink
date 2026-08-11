using Castlink.Application.Daily;

namespace Castlink.Api.Tests.Fakes;

internal sealed class FakeDailySubmissionRateLimiter : IDailySubmissionRateLimiter
{
    private readonly bool _allow;

    public FakeDailySubmissionRateLimiter(bool allow = true)
    {
        _allow = allow;
    }

    public Task<bool> TryAcquireAsync(DateOnly date, Guid playerId, CancellationToken cancellationToken) =>
        Task.FromResult(_allow);
}
