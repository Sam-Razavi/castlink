using Castlink.Application.Daily;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeCreditLookupRepository : ICreditLookupRepository
{
    private readonly IReadOnlySet<(int PersonId, int FilmId)> _knownCredits;

    public FakeCreditLookupRepository(IReadOnlySet<(int PersonId, int FilmId)> knownCredits)
    {
        _knownCredits = knownCredits;
    }

    public int CallCount { get; private set; }

    public Task<IReadOnlySet<(int PersonId, int FilmId)>> GetCreditsForFilmsAsync(
        IReadOnlyCollection<int> filmIds, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_knownCredits);
    }
}
