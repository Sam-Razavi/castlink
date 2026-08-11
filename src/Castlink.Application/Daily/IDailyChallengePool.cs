namespace Castlink.Application.Daily;

/// <summary>
/// The curated pool of people eligible to appear in a daily challenge — popular enough and
/// well-credited enough that both ends of the pair are actually recognisable (see docs/PLAN.md
/// Phase 4). Backed by a DB query, not the in-memory graph snapshot: eligibility depends on
/// <c>Person.Popularity</c>/<c>CreditCount</c>, neither of which the CSR graph carries.
/// </summary>
public interface IDailyChallengePool
{
    /// <summary>Eligible person ids, in a stable order — the generator indexes into this list by
    /// position, so the same pool snapshot must always yield the same id at a given index.</summary>
    Task<IReadOnlyList<int>> GetEligiblePersonIdsAsync(CancellationToken cancellationToken);
}
