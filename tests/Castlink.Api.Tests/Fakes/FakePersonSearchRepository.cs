using Castlink.Application.People;

namespace Castlink.Api.Tests.Fakes;

internal sealed class FakePersonSearchRepository : IPersonSearchRepository
{
    private readonly IReadOnlyList<PersonSearchResult> _results;

    public FakePersonSearchRepository(IReadOnlyList<PersonSearchResult> results)
    {
        _results = results;
    }

    public string? LastQuery { get; private set; }

    public int? LastLimit { get; private set; }

    public Task<IReadOnlyList<PersonSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        LastQuery = query;
        LastLimit = limit;
        return Task.FromResult(_results);
    }
}
