namespace Castlink.Application.People;

/// <summary>Backs actor-search typeahead. See docs/PLAN.md Phase 3.</summary>
public interface IPersonSearchRepository
{
    Task<IReadOnlyList<PersonSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}
