using Castlink.Application.Daily;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeDailySubmissionRateLimiter : IDailySubmissionRateLimiter
{
    private readonly bool _allow;

    public FakeDailySubmissionRateLimiter(bool allow = true)
    {
        _allow = allow;
    }

    public int CallCount { get; private set; }

    public Task<bool> TryAcquireAsync(DateOnly date, Guid playerId, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_allow);
    }
}
