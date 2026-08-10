namespace Castlink.Application.Graph;

public sealed record PathResult(PathOutcome Outcome, IReadOnlyList<PathLink> Links)
{
    /// <summary>Number of films used — the "N degrees of separation" count. Zero for the
    /// same-actor case (a real answer, not a failure).</summary>
    public int Degrees => Links.Count;

    public static PathResult Found(IReadOnlyList<PathLink> links) => new(PathOutcome.Found, links);

    public static PathResult NotFound(PathOutcome outcome)
    {
        if (outcome == PathOutcome.Found)
        {
            throw new ArgumentException("Use Found(...) for a successful result.", nameof(outcome));
        }

        return new PathResult(outcome, []);
    }
}
