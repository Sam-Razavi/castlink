using Castlink.Application.Daily;
using Castlink.Domain;
using Xunit;

namespace Castlink.Application.Tests.Daily;

/// <summary>
/// PLAN.md's literal Phase 4 test requirement: "validation rejects fabricated paths (non-existent
/// credit, non-contiguous chain, wrong endpoints)". Fixture: person 1 --film 10--> person 2
/// --film 20--> person 3, i.e. credits (1,10), (2,10), (2,20), (3,20).
/// </summary>
public sealed class DailyPathValidatorTests
{
    private static readonly IReadOnlySet<(int PersonId, int FilmId)> KnownCredits = new HashSet<(int, int)>
    {
        (1, 10), (2, 10), (2, 20), (3, 20),
    };

    private readonly DailyPathValidator _validator = new();

    [Fact]
    public void A_valid_contiguous_path_is_accepted()
    {
        var path = new[] { new DailyPathStepRecord(1, 10), new DailyPathStepRecord(2, 20), new DailyPathStepRecord(3, null) };

        var result = _validator.Validate(path, expectedFromPersonId: 1, expectedToPersonId: 3, KnownCredits);

        Assert.True(result.IsValid);
        Assert.Equal(PathValidationOutcome.Valid, result.Outcome);
        Assert.Equal([new PathLinkRecord(1, 10, 2), new PathLinkRecord(2, 20, 3)], result.Links);
    }

    [Fact]
    public void A_step_claiming_a_credit_that_does_not_exist_is_rejected()
    {
        var path = new[] { new DailyPathStepRecord(1, 999), new DailyPathStepRecord(3, null) };

        var result = _validator.Validate(path, expectedFromPersonId: 1, expectedToPersonId: 3, KnownCredits);

        Assert.False(result.IsValid);
        Assert.Equal(PathValidationOutcome.NonExistentCredit, result.Outcome);
        Assert.Empty(result.Links);
    }

    [Fact]
    public void A_non_contiguous_chain_that_skips_the_real_intermediate_person_is_rejected()
    {
        // Film 10 genuinely connects 1 and 2, but not 1 and 3 — a submission claiming film 10
        // jumps straight from 1 to 3 must fail even though (1, 10) is itself a real credit.
        var path = new[] { new DailyPathStepRecord(1, 10), new DailyPathStepRecord(3, null) };

        var result = _validator.Validate(path, expectedFromPersonId: 1, expectedToPersonId: 3, KnownCredits);

        Assert.False(result.IsValid);
        Assert.Equal(PathValidationOutcome.NonExistentCredit, result.Outcome);
    }

    [Fact]
    public void A_path_starting_at_the_wrong_person_is_rejected()
    {
        var path = new[] { new DailyPathStepRecord(2, 20), new DailyPathStepRecord(3, null) };

        var result = _validator.Validate(path, expectedFromPersonId: 1, expectedToPersonId: 3, KnownCredits);

        Assert.Equal(PathValidationOutcome.WrongStartPerson, result.Outcome);
    }

    [Fact]
    public void A_path_ending_at_the_wrong_person_is_rejected()
    {
        var path = new[] { new DailyPathStepRecord(1, 10), new DailyPathStepRecord(2, null) };

        var result = _validator.Validate(path, expectedFromPersonId: 1, expectedToPersonId: 3, KnownCredits);

        Assert.Equal(PathValidationOutcome.WrongEndPerson, result.Outcome);
    }

    [Fact]
    public void An_empty_path_is_rejected()
    {
        var result = _validator.Validate([], expectedFromPersonId: 1, expectedToPersonId: 3, KnownCredits);

        Assert.Equal(PathValidationOutcome.EmptyPath, result.Outcome);
    }
}
