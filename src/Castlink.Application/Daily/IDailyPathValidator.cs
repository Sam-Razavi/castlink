namespace Castlink.Application.Daily;

/// <summary>
/// Server-side validation of a submitted daily-challenge path against real credits — never trust a
/// client-reported chain (see docs/PLAN.md Phase 4). Pure: takes an already-fetched credit set
/// rather than querying itself, so it needs no EF/HTTP dependency and is trivially unit-testable.
/// </summary>
public interface IDailyPathValidator
{
    PathValidationResult Validate(
        IReadOnlyList<DailyPathStepRecord> submittedPath,
        int expectedFromPersonId,
        int expectedToPersonId,
        IReadOnlySet<(int PersonId, int FilmId)> knownCredits);
}
