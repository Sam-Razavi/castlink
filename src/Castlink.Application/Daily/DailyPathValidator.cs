using Castlink.Domain;

namespace Castlink.Application.Daily;

public sealed class DailyPathValidator : IDailyPathValidator
{
    public PathValidationResult Validate(
        IReadOnlyList<DailyPathStepRecord> submittedPath,
        int expectedFromPersonId,
        int expectedToPersonId,
        IReadOnlySet<(int PersonId, int FilmId)> knownCredits)
    {
        if (submittedPath.Count == 0)
        {
            return PathValidationResult.Invalid(PathValidationOutcome.EmptyPath);
        }

        if (submittedPath[0].PersonId != expectedFromPersonId)
        {
            return PathValidationResult.Invalid(PathValidationOutcome.WrongStartPerson);
        }

        if (submittedPath[^1].PersonId != expectedToPersonId)
        {
            return PathValidationResult.Invalid(PathValidationOutcome.WrongEndPerson);
        }

        var links = new List<PathLinkRecord>(submittedPath.Count - 1);
        for (var i = 0; i < submittedPath.Count - 1; i++)
        {
            var current = submittedPath[i];
            var next = submittedPath[i + 1];

            // Both people must actually hold a credit on the claimed film — one condition that
            // rejects a fabricated credit *and* a skipped (non-contiguous) hop identically, since a
            // skipped hop's film won't credit both ends either.
            if (current.FilmId is not { } filmId
                || !knownCredits.Contains((current.PersonId, filmId))
                || !knownCredits.Contains((next.PersonId, filmId)))
            {
                return PathValidationResult.Invalid(PathValidationOutcome.NonExistentCredit);
            }

            links.Add(new PathLinkRecord(current.PersonId, filmId, next.PersonId));
        }

        return PathValidationResult.Valid(links);
    }
}
