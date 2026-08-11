using Castlink.Application.Graph;

namespace Castlink.Application.Tests.Fakes;

/// <summary>Returns a scripted sequence of <see cref="PathResult"/>s, one per call, then a fixed
/// fallback once the script runs out — lets a test drive <c>DailyChallengeGenerator</c>'s
/// accept/reject/retry loop deterministically without depending on real BFS output or the exact
/// SHA256-seeded <see cref="Random"/> sequence.</summary>
internal sealed class FakePathFinder : IPathFinder
{
    private readonly Queue<PathResult> _scriptedResults;
    private readonly PathResult _fallback;

    public FakePathFinder(IEnumerable<PathResult> scriptedResults, PathResult? fallback = null)
    {
        _scriptedResults = new Queue<PathResult>(scriptedResults);
        _fallback = fallback ?? PathResult.NotFound(PathOutcome.NoConnectionWithinDepth);
    }

    public int CallCount { get; private set; }

    public PathResult FindPath(IGraphSnapshot graph, int fromPersonId, int toPersonId, int maxDepth = 6)
    {
        CallCount++;
        return _scriptedResults.Count > 0 ? _scriptedResults.Dequeue() : _fallback;
    }
}
