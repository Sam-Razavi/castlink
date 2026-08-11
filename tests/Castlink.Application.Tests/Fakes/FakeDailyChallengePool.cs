using Castlink.Application.Daily;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeDailyChallengePool : IDailyChallengePool
{
    private readonly IReadOnlyList<int> _personIds;

    public FakeDailyChallengePool(IReadOnlyList<int> personIds)
    {
        _personIds = personIds;
    }

    public Task<IReadOnlyList<int>> GetEligiblePersonIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_personIds);
}
