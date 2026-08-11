using Castlink.Application.Daily;

namespace Castlink.Api.Tests.Fakes;

internal sealed class FakeCreditLookupRepository : ICreditLookupRepository
{
    private readonly IReadOnlySet<(int PersonId, int FilmId)> _knownCredits;

    public FakeCreditLookupRepository(IReadOnlySet<(int PersonId, int FilmId)> knownCredits)
    {
        _knownCredits = knownCredits;
    }

    public Task<IReadOnlySet<(int PersonId, int FilmId)>> GetCreditsForFilmsAsync(
        IReadOnlyCollection<int> filmIds, CancellationToken cancellationToken) =>
        Task.FromResult(_knownCredits);
}
