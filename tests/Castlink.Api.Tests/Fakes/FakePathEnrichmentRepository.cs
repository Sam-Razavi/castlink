using Castlink.Application.Graph;

namespace Castlink.Api.Tests.Fakes;

internal sealed class FakePathEnrichmentRepository : IPathEnrichmentRepository
{
    private readonly IReadOnlyDictionary<int, PersonSummary> _people;
    private readonly IReadOnlyDictionary<int, FilmSummary> _films;

    public FakePathEnrichmentRepository(IReadOnlyDictionary<int, PersonSummary> people, IReadOnlyDictionary<int, FilmSummary> films)
    {
        _people = people;
        _films = films;
    }

    public Task<IReadOnlyDictionary<int, PersonSummary>> GetPeopleAsync(
        IReadOnlyCollection<int> personIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, PersonSummary>>(
            personIds.Where(_people.ContainsKey).ToDictionary(id => id, id => _people[id]));

    public Task<IReadOnlyDictionary<int, FilmSummary>> GetFilmsAsync(
        IReadOnlyCollection<int> filmIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, FilmSummary>>(
            filmIds.Where(_films.ContainsKey).ToDictionary(id => id, id => _films[id]));
}
