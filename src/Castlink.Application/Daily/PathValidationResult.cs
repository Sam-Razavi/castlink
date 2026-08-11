using Castlink.Domain;

namespace Castlink.Application.Daily;

public enum PathValidationOutcome
{
    Valid,
    EmptyPath,
    WrongStartPerson,
    WrongEndPerson,

    /// <summary>Covers both "this credit doesn't exist" and "the chain isn't actually contiguous" —
    /// a hop is only accepted if *both* people hold a credit on the claimed film, so a skipped link
    /// fails this the same way a fabricated one does.</summary>
    NonExistentCredit,
}

/// <summary>Mirrors <c>PathResult</c>'s Found/NotFound convention (see
/// <c>Castlink.Application.Graph.PathResult</c>) for the same reason: one type, a discriminated
/// outcome, and links only ever populated on success.</summary>
public sealed record PathValidationResult(PathValidationOutcome Outcome, IReadOnlyList<PathLinkRecord> Links)
{
    public int Length => Links.Count;

    public bool IsValid => Outcome == PathValidationOutcome.Valid;

    public static PathValidationResult Valid(IReadOnlyList<PathLinkRecord> links) => new(PathValidationOutcome.Valid, links);

    public static PathValidationResult Invalid(PathValidationOutcome outcome)
    {
        if (outcome == PathValidationOutcome.Valid)
        {
            throw new ArgumentException("Use Valid(...) for a successful result.", nameof(outcome));
        }

        return new PathValidationResult(outcome, []);
    }
}
